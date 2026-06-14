#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor.SceneState {
    /// <summary>
    /// Validation menu entries for checking and repairing StateRoot saveId uniqueness during development.
    /// </summary>
    public static class StateRootValidationMenu {
        [MenuItem("Tools/Scene State/Validate StateRoot Ids (Open Scenes)")]
        public static void ValidateOpenScenes() {
            var anyErrors = false;

            for (var i = 0; i < SceneManager.sceneCount; i++) {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path)) {
                    continue;
                }

                var errors = StateRootValidator.ValidateScene(scene);
                for (var e = 0; e < errors.Count; e++) {
                    anyErrors = true;
                    Debug.LogError(errors[e].Message, errors[e].Context);
                }
            }

            if (!anyErrors) {
                Debug.Log("StateRoot validation: OK (open scenes).");
            }
        }

        [MenuItem("Tools/Scene State/Fix Duplicate StateRoot Ids (Open Scenes)")]
        public static void FixDuplicatesInOpenScenes() {
            var totalFixed = 0;

            for (var i = 0; i < SceneManager.sceneCount; i++) {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path)) {
                    continue;
                }

                var fixedCount = StateRootIdAssigner.ReassignDuplicateIds(scene);
                if (fixedCount > 0) {
                    EditorSceneManager.MarkSceneDirty(scene);
                    totalFixed += fixedCount;
                    Debug.Log($"Reassigned {fixedCount} duplicate StateRoot id(s) in scene '{scene.name}'.");
                }
            }

            if (totalFixed == 0) {
                Debug.Log("Fix Duplicate StateRoot Ids: no duplicates found (open scenes).");
            } else {
                Debug.Log($"Fix Duplicate StateRoot Ids: reassigned {totalFixed} id(s). Save the scene(s) to persist.");
            }
        }
    }
}
#endif
