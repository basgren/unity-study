#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Features.Doors.Editor {
    [InitializeOnLoad]
    public static class EntrancePlayModeValidator {
        private const string EnabledKey = "Entrances.ValidationOnPlay.Enabled";

        static EntrancePlayModeValidator() {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Tools/Doors/Validation On Play/Entrances Enabled")]
        private static void ToggleEnabled() {
            var enabled = !IsEnabled();
            EditorPrefs.SetBool(EnabledKey, enabled);
            Menu.SetChecked("Tools/Doors/Validation On Play/Entrances Enabled", enabled);
        }

        [MenuItem("Tools/Doors/Validation On Play/Entrances Enabled", true)]
        private static bool ToggleEnabledValidate() {
            Menu.SetChecked("Tools/Doors/Validation On Play/Entrances Enabled", IsEnabled());
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

            var errors = ValidateOpenScenes();
            if (errors.Count == 0) {
                return;
            }

            for (var i = 0; i < errors.Count; i++) {
                Debug.LogError(errors[i].Message, errors[i].Context);
            }

            EditorApplication.delayCall += () => { EditorApplication.isPlaying = false; };
        }

        private static List<EntranceValidator.ValidationError> ValidateOpenScenes() {
            var all = new List<EntranceValidator.ValidationError>();

            for (var i = 0; i < EditorSceneManager.sceneCount; i++) {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded) {
                    continue;
                }

                var errors = EntranceValidator.ValidateScene(scene);
                all.AddRange(errors);
            }

            return all;
        }
    }
}
#endif
