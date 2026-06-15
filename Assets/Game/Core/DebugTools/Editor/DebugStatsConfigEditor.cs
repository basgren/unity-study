#if UNITY_EDITOR
using System;
using Game.Configs;
using Game.Core.Bootstrap;
using Game.Core.Models.Shop;
using Game.Features.Characters.Hero;
using UnityEditor;
using UnityEngine;

namespace Game.Core.DebugTools.Editor {
    /// <summary>
    /// Inspector for <see cref="DebugStatsConfig"/>. In edit mode it shows and edits the serialized
    /// initial-stats list (pre-levelled at game start). In play mode it shows and edits the live
    /// hero stat levels from <see cref="PlayerState"/> instead, so the inspector reflects actual
    /// upgrades (e.g. those bought at a BlessingTotem) and edits apply to the hero immediately.
    /// Mirrors <see cref="DebugFlagConfigEditor"/> and the debug inventory inspector.
    /// </summary>
    [CustomEditor(typeof(DebugStatsConfig))]
    public sealed class DebugStatsConfigEditor : UnityEditor.Editor {
        private const string MainConfigResource = "MainConfig";

        private StatId addStat;
        private int addLevel = 1;

        // Live play-mode stat levels can change at any frame (e.g. a totem purchase); keep in sync.
        public override bool RequiresConstantRepaint() {
            return Application.isPlaying;
        }

        public override void OnInspectorGUI() {
            DrawHintBox();
            EditorGUILayout.Space();

            if (Application.isPlaying) {
                DrawPlayModeStats();
            } else {
                DrawEditModeStats();
                EditorGUILayout.Space();
                DrawAddRow();
            }
        }

        private void DrawHintBox() {
            EditorGUILayout.HelpBox(
                "Edit mode: this list pre-levels hero stats when the game starts.\n" +
                "Play mode: shows and edits the live hero stat levels (changes apply immediately).",
                MessageType.None);

            var config = Resources.Load<MainConfig>(MainConfigResource);
            var enabled = config != null && config.DebugSystems != null && config.DebugSystems.EnableDebugStats;

            using (new EditorGUILayout.HorizontalScope()) {
                if (enabled) {
                    EditorGUILayout.HelpBox("Status: ENABLED via MainConfig → Debug Systems.", MessageType.Info);
                } else {
                    EditorGUILayout.HelpBox(
                        "Status: DISABLED. Enable it in MainConfig → Debug Systems → Enable Debug Stats.",
                        MessageType.Warning);
                }

                if (config != null && GUILayout.Button("Ping\nMainConfig", GUILayout.Width(90), GUILayout.Height(38))) {
                    Selection.activeObject = config;
                    EditorGUIUtility.PingObject(config);
                }
            }
        }

        private void DrawEditModeStats() {
            serializedObject.Update();
            var statsProp = serializedObject.FindProperty("initialStats");

            EditorGUILayout.LabelField("Initial Stats", EditorStyles.boldLabel);

            if (statsProp.arraySize == 0) {
                EditorGUILayout.LabelField("  <empty>");
            }

            var removeIndex = -1;
            var clearAll = false;

            for (var i = 0; i < statsProp.arraySize; i++) {
                var element = statsProp.GetArrayElementAtIndex(i);
                var idProp = element.FindPropertyRelative("statId");
                var levelProp = element.FindPropertyRelative("level");

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox)) {
                    EditorGUILayout.LabelField(((StatId)idProp.enumValueIndex).ToString());
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField("Lv", GUILayout.Width(20));
                    levelProp.intValue = Mathf.Max(0, EditorGUILayout.IntField(levelProp.intValue, GUILayout.Width(50)));

                    if (GUILayout.Button("X", GUILayout.Width(24))) {
                        removeIndex = i;
                    }
                }
            }

            if (statsProp.arraySize > 0 && GUILayout.Button("Clear All")) {
                clearAll = true;
            }

            if (clearAll) {
                statsProp.ClearArray();
            } else if (removeIndex >= 0) {
                statsProp.DeleteArrayElementAtIndex(removeIndex);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPlayModeStats() {
            EditorGUILayout.LabelField("Live Hero Stat Levels", EditorStyles.boldLabel);

            var state = GetLiveState();
            if (state == null) {
                EditorGUILayout.HelpBox("Live player state not available yet (not initialized).", MessageType.Info);
                return;
            }

            // Stats are a fixed enum, so list every one with its live level (0 = not upgraded).
            // This is the single source of truth shared by every BlessingTotem.
            foreach (StatId stat in Enum.GetValues(typeof(StatId))) {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox)) {
                    EditorGUILayout.LabelField(stat.ToString());
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField("Lv", GUILayout.Width(20));

                    var current = state.GetStatLevel(stat);
                    var edited = Mathf.Max(0, EditorGUILayout.IntField(current, GUILayout.Width(50)));
                    if (edited != current) {
                        SetLiveStatLevel(state, stat, edited);
                    }
                }
            }
        }

        private void DrawAddRow() {
            EditorGUILayout.LabelField("Add / Set Stat", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope()) {
                addStat = (StatId)EditorGUILayout.EnumPopup(addStat);
                EditorGUILayout.LabelField("Lv", GUILayout.Width(20));
                addLevel = Mathf.Max(0, EditorGUILayout.IntField(addLevel, GUILayout.Width(50)));

                if (GUILayout.Button("Add", GUILayout.Width(60))) {
                    AddOrUpdateStat(addStat, addLevel);
                }
            }
        }

        private void AddOrUpdateStat(StatId stat, int level) {
            serializedObject.Update();
            var statsProp = serializedObject.FindProperty("initialStats");

            for (var i = 0; i < statsProp.arraySize; i++) {
                var element = statsProp.GetArrayElementAtIndex(i);
                if ((StatId)element.FindPropertyRelative("statId").enumValueIndex == stat) {
                    element.FindPropertyRelative("level").intValue = level;
                    serializedObject.ApplyModifiedProperties();
                    return;
                }
            }

            statsProp.arraySize++;
            var newElement = statsProp.GetArrayElementAtIndex(statsProp.arraySize - 1);
            newElement.FindPropertyRelative("statId").enumValueIndex = (int)stat;
            newElement.FindPropertyRelative("level").intValue = level;
            serializedObject.ApplyModifiedProperties();
        }

        private static void SetLiveStatLevel(PlayerState state, StatId stat, int level) {
            state.SetStatLevel(stat, level);

            // Push to the live hero so max health / melee damage take effect now (mirrors StatShop).
            // Throw damage is read per-spawn, so it needs no push.
            var hero = G.Hero != null ? G.Hero.Controller : null;
            if (hero != null) {
                hero.ApplyCurrentStats();
            }
        }

        private static PlayerState GetLiveState() {
            return G.Game != null ? G.Game.playerState : null;
        }
    }
}
#endif
