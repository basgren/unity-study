#if UNITY_EDITOR
using System.Collections.Generic;
using Game.Configs;
using Game.Core.Bootstrap;
using Game.Features.Characters.Hero;
using UnityEditor;
using UnityEngine;

namespace Game.Core.DebugTools.Editor {
    /// <summary>
    /// Inspector for <see cref="DebugFlagConfig"/>. In edit mode it shows and edits the serialized
    /// initial-flags list (raised at game start). In play mode it shows and edits the live player
    /// flag set instead, so the inspector reflects the actual flag state and edits act like
    /// raising/clearing a flag.
    /// </summary>
    [CustomEditor(typeof(DebugFlagConfig))]
    public sealed class DebugFlagConfigEditor : UnityEditor.Editor {
        private const string MainConfigResource = "MainConfig";

        private string addFlag = string.Empty;

        // Live play-mode flags can change at any frame; keep the inspector in sync.
        public override bool RequiresConstantRepaint() {
            return Application.isPlaying;
        }

        public override void OnInspectorGUI() {
            DrawHintBox();
            EditorGUILayout.Space();

            if (Application.isPlaying) {
                DrawPlayModeFlags();
            } else {
                DrawEditModeFlags();
            }

            EditorGUILayout.Space();
            DrawAddRow();
        }

        private void DrawHintBox() {
            EditorGUILayout.HelpBox(
                "Edit mode: this list raises player flags when the game starts.\n" +
                "Play mode: shows and edits the live player flags (changes act like raising/clearing).",
                MessageType.None);

            var config = Resources.Load<MainConfig>(MainConfigResource);
            var enabled = config != null && config.DebugSystems != null && config.DebugSystems.EnableDebugFlags;

            using (new EditorGUILayout.HorizontalScope()) {
                if (enabled) {
                    EditorGUILayout.HelpBox("Status: ENABLED via MainConfig → Debug Systems.", MessageType.Info);
                } else {
                    EditorGUILayout.HelpBox(
                        "Status: DISABLED. Enable it in MainConfig → Debug Systems → Enable Debug Flags.",
                        MessageType.Warning);
                }

                if (config != null && GUILayout.Button("Ping\nMainConfig", GUILayout.Width(90), GUILayout.Height(38))) {
                    Selection.activeObject = config;
                    EditorGUIUtility.PingObject(config);
                }
            }
        }

        private void DrawEditModeFlags() {
            serializedObject.Update();
            var flagsProp = serializedObject.FindProperty("initialFlags");

            EditorGUILayout.LabelField("Initial Flags", EditorStyles.boldLabel);

            if (flagsProp.arraySize == 0) {
                EditorGUILayout.LabelField("  <empty>");
            }

            var removeIndex = -1;
            var clearAll = false;

            for (var i = 0; i < flagsProp.arraySize; i++) {
                var element = flagsProp.GetArrayElementAtIndex(i);
                if (DrawFlagRow(element.stringValue)) {
                    removeIndex = i;
                }
            }

            if (flagsProp.arraySize > 0 && GUILayout.Button("Clear All")) {
                clearAll = true;
            }

            if (clearAll) {
                flagsProp.ClearArray();
            } else if (removeIndex >= 0) {
                flagsProp.DeleteArrayElementAtIndex(removeIndex);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPlayModeFlags() {
            EditorGUILayout.LabelField("Live Player Flags", EditorStyles.boldLabel);

            var state = GetLiveState();
            if (state == null) {
                EditorGUILayout.HelpBox("Live player state not available yet (not initialized).", MessageType.Info);
                return;
            }

            var flags = state.Flags;
            if (flags.Count == 0) {
                EditorGUILayout.LabelField("  <none set>");
            }

            string removeFlag = null;
            var clearAll = false;

            for (var i = 0; i < flags.Count; i++) {
                if (DrawFlagRow(flags[i])) {
                    removeFlag = flags[i];
                }
            }

            if (flags.Count > 0 && GUILayout.Button("Clear All")) {
                clearAll = true;
            }

            // Apply mutations after drawing so the flags list is not modified mid-iteration.
            if (clearAll) {
                var snapshot = new List<string>(flags);
                foreach (var flag in snapshot) {
                    state.ClearFlag(flag);
                }
            } else if (removeFlag != null) {
                state.ClearFlag(removeFlag);
            }
        }

        private void DrawAddRow() {
            EditorGUILayout.LabelField("Add Flag", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope()) {
                addFlag = EditorGUILayout.TextField(addFlag);

                if (GUILayout.Button("Add", GUILayout.Width(60))) {
                    AddFlag(addFlag);
                    addFlag = string.Empty;
                    GUI.FocusControl(null);
                }
            }
        }

        private static bool DrawFlagRow(string flag) {
            var remove = false;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox)) {
                EditorGUILayout.LabelField(string.IsNullOrEmpty(flag) ? "<empty>" : flag);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("X", GUILayout.Width(24))) {
                    remove = true;
                }
            }

            return remove;
        }

        private void AddFlag(string flag) {
            if (string.IsNullOrWhiteSpace(flag)) {
                return;
            }

            flag = flag.Trim();

            if (Application.isPlaying) {
                var state = GetLiveState();
                if (state != null) {
                    state.SetFlag(flag);
                }

                return;
            }

            // Edit mode: append to the serialized initial-flags list, skipping duplicates.
            serializedObject.Update();
            var flagsProp = serializedObject.FindProperty("initialFlags");

            for (var i = 0; i < flagsProp.arraySize; i++) {
                if (flagsProp.GetArrayElementAtIndex(i).stringValue == flag) {
                    return;
                }
            }

            flagsProp.arraySize++;
            flagsProp.GetArrayElementAtIndex(flagsProp.arraySize - 1).stringValue = flag;
            serializedObject.ApplyModifiedProperties();
        }

        private static PlayerState GetLiveState() {
            return G.Game != null ? G.Game.playerState : null;
        }
    }
}
#endif
