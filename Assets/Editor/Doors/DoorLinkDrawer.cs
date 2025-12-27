#if UNITY_EDITOR
using System;
using Doors;
using UnityEditor;
using UnityEngine;

namespace Editor.Doors {
    /// <summary>
    /// Editor drawer that renders DoorLink as:
    /// - Target Scene (SceneAsset)
    /// - Target Door (popup list of door IDs inside that scene)
    /// </summary>
    [CustomPropertyDrawer(typeof(DoorLink))]
    public sealed class DoorLinkDrawer : PropertyDrawer {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            return EditorGUIUtility.singleLineHeight * 2f + 2f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            var sceneProp = property.FindPropertyRelative("targetScene");
            var doorIdProp = property.FindPropertyRelative("targetDoorId");

            var lineH = EditorGUIUtility.singleLineHeight;
            var r1 = new Rect(position.x, position.y, position.width, lineH);
            var r2 = new Rect(position.x, position.y + lineH + 2f, position.width, lineH);

            EditorGUI.BeginProperty(position, label, property);

            EditorGUI.PropertyField(r1, sceneProp, new GUIContent("Target Scene"));

            var guidProp = sceneProp.FindPropertyRelative("sceneGuid");
            var sceneGuid = guidProp != null ? guidProp.stringValue : string.Empty;

            var currentDoor = property.serializedObject.targetObject as Door;
            var currentDoorId = currentDoor != null ? currentDoor.DoorId : string.Empty;
            var currentSceneGuid = string.Empty;

            if (currentDoor != null && currentDoor.gameObject != null) {
                currentSceneGuid = DoorEditorUtils.GetSceneGuid(currentDoor.gameObject.scene.path);
            }
            
            if (string.IsNullOrWhiteSpace(sceneGuid)) {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUI.TextField(r2, "Target Door", "<select scene first>");
                EditorGUI.EndDisabledGroup();
                EditorGUI.EndProperty();
                return;
            }

            var doors = SceneDoorCache.GetDoorsByGuid(sceneGuid);
            var currentTargetId = doorIdProp.stringValue;
            var names = new System.Collections.Generic.List<string>(doors.Length + 1);
            var values = new System.Collections.Generic.List<string>(doors.Length + 1);

            names.Add("(None)");
            values.Add(string.Empty);

            var selectedIndex = 0;

            for (var i = 0; i < doors.Length; i++) {
                var id = doors[i].DoorId;

                if (!string.IsNullOrWhiteSpace(currentSceneGuid) &&
                    string.Equals(sceneGuid, currentSceneGuid, StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(currentDoorId) &&
                    string.Equals(id, currentDoorId, StringComparison.Ordinal)) {
                    continue;
                }

                names.Add(doors[i].Label);
                values.Add(id);

                if (!string.IsNullOrWhiteSpace(currentTargetId) &&
                    string.Equals(currentTargetId, id, StringComparison.Ordinal)) {
                    selectedIndex = values.Count - 1;
                }
            }

            var newIndex = EditorGUI.Popup(r2, "Target Door", selectedIndex, names.ToArray());
            var newValue = values[newIndex];

            if (newValue != currentTargetId) {
                doorIdProp.stringValue = newValue;
                property.serializedObject.ApplyModifiedProperties();
            }

            if (!string.IsNullOrWhiteSpace(currentTargetId) && selectedIndex == 0) {
                var warnRect = new Rect(r2.xMax - 180f, r2.y, 180f, r2.height);
                EditorGUI.LabelField(warnRect, "Missing target door", EditorStyles.miniBoldLabel);
            }

            EditorGUI.EndProperty();
        }
    }
}
#endif
