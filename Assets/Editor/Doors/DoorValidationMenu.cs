#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Editor.Doors {
    /// <summary>
    /// Validation menu entry for quick checks during development.
    /// </summary>
    public static class DoorValidationMenu {
        [MenuItem("Tools/Doors/Validate Open Scenes")]
        public static void ValidateOpenScenes() {
            var anyErrors = false;

            for (var i = 0; i < SceneManager.sceneCount; i++) {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path)) {
                    continue;
                }

                var errors = DoorValidator.ValidateScene(scene);
                for (var e = 0; e < errors.Count; e++) {
                    anyErrors = true;
                    Debug.LogError(errors[e].Message, errors[e].Context);
                }
            }

            if (!anyErrors) {
                Debug.Log("Doors validation: OK (open scenes).");
            }
        }
    }
}
#endif
