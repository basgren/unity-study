#if UNITY_EDITOR
using Doors;
using UnityEditor;
using UnityEngine;

namespace Editor.Doors {
    /// <summary>
    /// Custom inspector for Door that shows a read-only DoorId and editor actions.
    /// </summary>
    [CustomEditor(typeof(Door))]
    public sealed class DoorEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            var door = (Door)target;

            EditorGUILayout.LabelField("Door", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Door ID", door.DoorId);
            EditorGUI.EndDisabledGroup();

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Copy ID")) {
                    EditorGUIUtility.systemCopyBuffer = door.DoorId;
                }

                if (GUILayout.Button("Change ID")) {
                    DoorChangeIdWindow.Show(door);
                }
            }

            EditorGUILayout.Space();

            DrawPropertiesExcluding(serializedObject, "m_Script", "doorId");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
