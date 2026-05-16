#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Game.Features.Doors.Editor {
    /// <summary>
    /// Custom inspector for Entrance that shows a read-only EntranceId and editor actions.
    /// </summary>
    [CustomEditor(typeof(Entrance))]
    public sealed class EntranceEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            var entrance = (Entrance)target;

            EditorGUILayout.LabelField("Entrance", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Entrance ID", entrance.EntranceId);
            EditorGUI.EndDisabledGroup();

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Copy ID")) {
                    EditorGUIUtility.systemCopyBuffer = entrance.EntranceId;
                }

                if (GUILayout.Button("Change ID")) {
                    EntranceChangeIdWindow.Show(entrance);
                }
            }

            EditorGUILayout.Space();

            DrawPropertiesExcluding(serializedObject, "m_Script", "entranceId");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
