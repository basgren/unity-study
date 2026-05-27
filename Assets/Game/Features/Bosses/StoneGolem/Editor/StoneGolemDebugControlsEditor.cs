using System.Collections.Generic;
using Game.Features.Bosses._Shared;
using UnityEditor;
using UnityEngine;

namespace Game.Features.Bosses.StoneGolem.Editor {
    /// <summary>
    /// Inspector for <see cref="StoneGolemDebugControls"/>. In play mode it draws movement buttons
    /// plus a button per <see cref="EnemyAction"/> found under the golem. Action and move buttons
    /// are enabled only while the golem is free (<see cref="StoneGolemDebugControls.CanAct"/>); all
    /// commands route through the component, which suspends the selector on use.
    /// </summary>
    [CustomEditor(typeof(StoneGolemDebugControls))]
    public class StoneGolemDebugControlsEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            DrawDefaultInspector();

            var debug = (StoneGolemDebugControls)target;

            EditorGUILayout.Space();
            if (!Application.isPlaying) {
                EditorGUILayout.HelpBox("Enter Play Mode to use the debug controls.", MessageType.Info);
                return;
            }

            DrawControlState(debug);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Movement", EditorStyles.boldLabel);
            DrawMovement(debug);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            DrawActions(debug);

            // Keep the enabled-state of the buttons live as IsBusy flips during play.
            Repaint();
        }

        private static void DrawControlState(StoneGolemDebugControls debug) {
            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField("Control", debug.AiEnabled ? "AI (autonomous)" : "Manual (debug)");
                EditorGUILayout.LabelField("Busy", debug.Golem != null && debug.Golem.IsBusy ? "yes" : "no");
            }

            using (new EditorGUI.DisabledScope(debug.AiEnabled)) {
                if (GUILayout.Button("Resume AI")) {
                    debug.EnableAi();
                }
            }
        }

        private static void DrawMovement(StoneGolemDebugControls debug) {
            using (new EditorGUI.DisabledScope(!debug.CanAct)) {
                using (new EditorGUILayout.HorizontalScope()) {
                    if (GUILayout.Button("Move Left")) {
                        debug.MoveLeft();
                    }

                    if (GUILayout.Button("Move Right")) {
                        debug.MoveRight();
                    }
                }
            }

            // Stop is always available so a runaway debug move can be halted even mid-action.
            if (GUILayout.Button("Stop")) {
                debug.Stop();
            }
        }

        private static void DrawActions(StoneGolemDebugControls debug) {
            IReadOnlyList<EnemyAction> actions = debug.RefreshActions();
            if (actions.Count == 0) {
                EditorGUILayout.HelpBox("No EnemyAction components found under this object.", MessageType.None);
                return;
            }

            bool canAct = debug.CanAct;
            for (int i = 0; i < actions.Count; i++) {
                EnemyAction action = actions[i];
                if (action == null) {
                    continue;
                }

                using (new EditorGUI.DisabledScope(!canAct)) {
                    if (GUILayout.Button(action.gameObject.name)) {
                        debug.Run(action);
                    }
                }
            }
        }
    }
}
