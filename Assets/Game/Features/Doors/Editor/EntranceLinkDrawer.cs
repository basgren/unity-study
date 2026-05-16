#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game.Doors.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.Features.Doors.Editor {
    /// <summary>
    /// Editor drawer that renders EntranceLink as:
    /// - Target Scene (SceneAsset)
    /// - Target Entrance (popup list of entrance IDs inside that scene)
    /// </summary>
    [CustomPropertyDrawer(typeof(EntranceLink))]
    public sealed class EntranceLinkDrawer : PropertyDrawer {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            return EditorGUIUtility.singleLineHeight * 2f + 2f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            var sceneProp = property.FindPropertyRelative("targetScene");
            var entranceIdProp = property.FindPropertyRelative("targetEntranceId");

            var lineH = EditorGUIUtility.singleLineHeight;
            var r1 = new Rect(position.x, position.y, position.width, lineH);
            var r2 = new Rect(position.x, position.y + lineH + 2f, position.width, lineH);

            EditorGUI.BeginProperty(position, label, property);

            EditorGUI.PropertyField(r1, sceneProp, new GUIContent("Target Scene"));

            var guidProp = sceneProp.FindPropertyRelative("sceneGuid");
            var sceneGuid = guidProp != null ? guidProp.stringValue : string.Empty;

            var currentEntrance = property.serializedObject.targetObject as Entrance;
            var currentEntranceId = currentEntrance != null ? currentEntrance.EntranceId : string.Empty;
            var currentSceneGuid = string.Empty;

            if (currentEntrance != null && currentEntrance.gameObject != null) {
                currentSceneGuid = DoorEditorUtils.GetSceneGuid(currentEntrance.gameObject.scene.path);
            }

            if (string.IsNullOrWhiteSpace(sceneGuid)) {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUI.TextField(r2, "Target Entrance", "<select scene first>");
                EditorGUI.EndDisabledGroup();
                EditorGUI.EndProperty();
                return;
            }

            // SceneEntranceCache cannot OpenScene during play mode, so the dropdown would be empty
            // and incorrectly report a missing target. Show the stored id as a read-only field instead.
            if (Application.isPlaying) {
                EditorGUI.BeginDisabledGroup(true);
                var displayValue = string.IsNullOrEmpty(entranceIdProp.stringValue)
                    ? "(None)"
                    : entranceIdProp.stringValue;
                EditorGUI.TextField(r2, "Target Entrance", displayValue);
                EditorGUI.EndDisabledGroup();
                EditorGUI.EndProperty();
                return;
            }

            var entrances = SceneEntranceCache.GetEntrancesByGuid(sceneGuid);
            var currentTargetId = entranceIdProp.stringValue;
            var names = new List<string>(entrances.Length + 1);
            var values = new List<string>(entrances.Length + 1);

            names.Add("(None)");
            values.Add(string.Empty);

            var selectedIndex = 0;

            for (var i = 0; i < entrances.Length; i++) {
                var id = entrances[i].EntranceId;

                if (!string.IsNullOrWhiteSpace(currentSceneGuid) &&
                    string.Equals(sceneGuid, currentSceneGuid, StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(currentEntranceId) &&
                    string.Equals(id, currentEntranceId, StringComparison.Ordinal)) {
                    continue;
                }

                names.Add(entrances[i].Label);
                values.Add(id);

                if (!string.IsNullOrWhiteSpace(currentTargetId) &&
                    string.Equals(currentTargetId, id, StringComparison.Ordinal)) {
                    selectedIndex = values.Count - 1;
                }
            }

            var showMissingWarning = !string.IsNullOrWhiteSpace(currentTargetId) && selectedIndex == 0;
            var popupRect = r2;
            const float WarnWidth = 200f;

            if (showMissingWarning) {
                popupRect.width = Mathf.Max(0f, r2.width - WarnWidth);
            }

            var newIndex = EditorGUI.Popup(popupRect, "Target Entrance", selectedIndex, names.ToArray());

            // Only write back when the USER changed the selection. Popup returns the passed-in
            // selectedIndex when nothing was clicked this frame; comparing newValue vs currentTargetId
            // would clear the saved id whenever the scene cache returns empty (e.g. during play-mode
            // entry, see SceneEntranceCache.GetEntrancesByGuid), because selectedIndex stays at 0 ("None").
            if (newIndex != selectedIndex) {
                entranceIdProp.stringValue = values[newIndex];
                property.serializedObject.ApplyModifiedProperties();
            }

            if (showMissingWarning) {
                var warnRect = new Rect(r2.xMax - WarnWidth, r2.y, WarnWidth, r2.height);
                EditorGUI.LabelField(warnRect, "Missing target entrance", EditorStyles.miniBoldLabel);
            }

            EditorGUI.EndProperty();
        }
    }
}
#endif
