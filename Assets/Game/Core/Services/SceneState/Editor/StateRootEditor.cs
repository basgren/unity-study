#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Game.Core.Services.SceneState.Editor {
    /// <summary>
    /// Custom inspector for StateRoot that shows a read-only Save ID.
    /// On a prefab asset the ID is intentionally empty — each scene instance gets its own.
    /// A "Clear" button is offered on scene instances to repair a duplicate id: clearing it lets
    /// StateRootIdAssigner regenerate a fresh unique id on the next scene save.
    /// </summary>
    [CustomEditor(typeof(StateRoot))]
    public sealed class StateRootEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            serializedObject.Update();

            var root = (StateRoot)target;
            var isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(root);

            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUI.BeginDisabledGroup(true);
                if (isPrefabAsset) {
                    EditorGUILayout.TextField("Save ID", "(assigned per scene instance)");
                } else {
                    EditorGUILayout.TextField("Save ID", root.SaveId);
                }
                EditorGUI.EndDisabledGroup();

                // Clearing the id (then saving the scene) is the manual fix for a duplicate saveId:
                // the on-save assigner refills the empty id with a fresh unique one.
                if (!isPrefabAsset && GUILayout.Button("Clear", GUILayout.Width(50f))) {
                    serializedObject.FindProperty("saveId").stringValue = string.Empty;
                }
            }

            DrawPropertiesExcluding(serializedObject, "m_Script", "saveId");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
