#if UNITY_EDITOR
using Game.Features.Portals.Common.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Features.Portals.Entrance.Editor {
    /// <summary>
    /// Validation menu entry for quick entrance checks during development.
    /// </summary>
    public static class EntranceValidationMenu {
        [MenuItem("Tools/Portals/Entrances/Validate Open Scenes")]
        public static void ValidateOpenScenes() {
            var kind = PortalKindRegistry.GetForType(typeof(Entrance));
            if (kind == null) {
                Debug.LogWarning("Entrance kind is not registered yet.");
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
                Debug.Log("Entrances validation: OK (open scenes).");
            }
        }
    }
}
#endif
