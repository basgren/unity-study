#if UNITY_EDITOR
using Game.Features.Portals.Common.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Features.Portals.Doors.Editor {
    /// <summary>
    /// Validation menu entry for quick door checks during development.
    /// </summary>
    public static class DoorValidationMenu {
        [MenuItem("Tools/Portals/Doors/Validate Open Scenes")]
        public static void ValidateOpenScenes() {
            var kind = PortalKindRegistry.GetForType(typeof(Door));
            if (kind == null) {
                Debug.LogWarning("Door kind is not registered yet.");
                return;
            }

            var anyErrors = false;
            for (var i = 0; i < SceneManager.sceneCount; i++) {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path)) {
                    continue;
                }

                var errors = PortalValidator.ValidateScene(kind, scene);
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
