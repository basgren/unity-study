#if UNITY_EDITOR
using System.Collections.Generic;
using Game.Features.Portals.Common.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Features.Portals.Entrance.Editor {
    [InitializeOnLoad]
    public static class EntrancePlayModeValidator {
        private const string EnabledKey = "Entrances.ValidationOnPlay.Enabled";
        private const string MenuPath = "Tools/Portals/Entrances/Validation On Play Enabled";

        static EntrancePlayModeValidator() {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem(MenuPath)]
        private static void ToggleEnabled() {
            var enabled = !IsEnabled();
            EditorPrefs.SetBool(EnabledKey, enabled);
            Menu.SetChecked(MenuPath, enabled);
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleEnabledValidate() {
            Menu.SetChecked(MenuPath, IsEnabled());
            return true;
        }

        private static bool IsEnabled() {
            return EditorPrefs.GetBool(EnabledKey, true);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state) {
            if (!IsEnabled()) {
                return;
            }

            // Best moment: right before switching to Play (still in Edit Mode).
            if (state != PlayModeStateChange.ExitingEditMode) {
                return;
            }

            var kind = PortalKindRegistry.GetForType(typeof(Entrance));
            if (kind == null) {
                return;
            }

            var errors = new List<PortalValidationError>();
            for (var i = 0; i < EditorSceneManager.sceneCount; i++) {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded) {
                    continue;
                }

                errors.AddRange(PortalValidator.ValidateScene(kind, scene));
            }

            if (errors.Count == 0) {
                return;
            }

            for (var i = 0; i < errors.Count; i++) {
                Debug.LogError(errors[i].Message, errors[i].Context);
            }

            EditorApplication.delayCall += () => { EditorApplication.isPlaying = false; };
        }
    }
}
#endif
