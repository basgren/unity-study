using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Editor.SpriteSheetAnimationImporter {
    /// <summary>
    /// Creates or updates an AnimatorController with one state per generated clip, and wires
    /// user-provided comma-separated draft transitions. Does not invent conditions or triggers.
    /// </summary>
    public static class SpriteSheetAnimatorControllerBuilder {
        /// <summary>
        /// Summary of the controller build so the processor can show it in the console/summary dialog.
        /// </summary>
        public sealed class BuildReport {
            public AnimatorController controller;
            public int createdStates;
            public int createdTransitions;
            public readonly List<string> unknownTargets = new List<string>();
        }

        /// <summary>
        /// Creates the controller (or opens an existing one), ensures states for every provided clip,
        /// sets a reasonable default state, and adds missing draft transitions.
        /// </summary>
        public static BuildReport BuildOrUpdate(
            SpriteSheetImportSettings settings,
            string outputFolder,
            Dictionary<string, AnimationClip> clipsByState) {
            var report = new BuildReport();

            var controllerPath = $"{outputFolder}/{settings.controllerName}.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            }

            report.controller = controller;

            var rootMachine = controller.layers[0].stateMachine;

            // Create one state per clip, reusing existing states with the same name.
            var statesByName = new Dictionary<string, AnimatorState>();
            for (int i = 0; i < rootMachine.states.Length; i++) {
                statesByName[rootMachine.states[i].state.name] = rootMachine.states[i].state;
            }

            for (int row = 0; row < settings.stateNames.Count; row++) {
                var stateName = settings.stateNames[row];
                if (!clipsByState.TryGetValue(stateName, out var clip)) {
                    continue;
                }

                if (!statesByName.TryGetValue(stateName, out var state)) {
                    state = rootMachine.AddState(stateName);
                    statesByName[stateName] = state;
                    report.createdStates++;
                }

                state.motion = clip;
            }

            // Pick a reasonable default state: prefer "Idle", else first generated state, else keep existing.
            var defaultState = PickDefaultState(statesByName, settings.stateNames);
            if (defaultState != null) {
                rootMachine.defaultState = defaultState;
            }

            // Draft transitions.
            BuildDraftTransitions(settings, statesByName, report);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return report;
        }

        private static AnimatorState PickDefaultState(
            Dictionary<string, AnimatorState> statesByName,
            List<string> stateNames) {
            if (statesByName.TryGetValue("Idle", out var idle)) {
                return idle;
            }

            for (int i = 0; i < stateNames.Count; i++) {
                if (statesByName.TryGetValue(stateNames[i], out var first)) {
                    return first;
                }
            }

            return null;
        }

        private static void BuildDraftTransitions(
            SpriteSheetImportSettings settings,
            Dictionary<string, AnimatorState> statesByName,
            BuildReport report) {
            if (settings.transitionsTo == null) {
                return;
            }

            for (int row = 0; row < settings.stateNames.Count; row++) {
                if (row >= settings.transitionsTo.Count) {
                    break;
                }

                var sourceName = settings.stateNames[row];
                if (!statesByName.TryGetValue(sourceName, out var source)) {
                    continue;
                }

                var targets = ParseTargets(settings.transitionsTo[row]);
                for (int t = 0; t < targets.Count; t++) {
                    var targetName = targets[t];

                    if (!statesByName.TryGetValue(targetName, out var target)) {
                        report.unknownTargets.Add($"{sourceName} -> {targetName}");
                        continue;
                    }

                    if (HasTransition(source, target)) {
                        continue;
                    }

                    var transition = source.AddTransition(target);
                    transition.hasExitTime = true;
                    transition.exitTime = 1f;
                    transition.duration = 0f;
                    transition.hasFixedDuration = true;
                    transition.canTransitionToSelf = false;

                    report.createdTransitions++;
                }
            }
        }

        private static bool HasTransition(AnimatorState source, AnimatorState target) {
            var existing = source.transitions;
            for (int i = 0; i < existing.Length; i++) {
                if (existing[i].destinationState == target) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Splits a comma-separated string into trimmed, non-empty target names.
        /// </summary>
        private static List<string> ParseTargets(string raw) {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(raw)) {
                return list;
            }

            var parts = raw.Split(',');
            for (int i = 0; i < parts.Length; i++) {
                var trimmed = parts[i].Trim();
                if (trimmed.Length > 0) {
                    list.Add(trimmed);
                }
            }

            return list;
        }
    }
}
