#if UNITY_EDITOR
using UnityEditor;

namespace Game.Core.Services.SceneState.Editor {
    /// <summary>
    /// Custom inspector for StateRoot that shows a read-only Save ID.
    /// On a prefab asset the ID is intentionally empty — each scene instance gets its own.
    /// </summary>
    [CustomEditor(typeof(StateRoot))]
    public sealed class StateRootEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            serializedObject.Update();

            var root = (StateRoot)target;
            var isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(root);

            EditorGUI.BeginDisabledGroup(true);
            if (isPrefabAsset) {
                EditorGUILayout.TextField("Save ID", "(assigned per scene instance)");
            } else {
                EditorGUILayout.TextField("Save ID", root.SaveId);
            }
            EditorGUI.EndDisabledGroup();

            DrawPropertiesExcluding(serializedObject, "m_Script", "saveId");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
