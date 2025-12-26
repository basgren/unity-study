#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Editor.Doors {
    [InitializeOnLoad]
    public static class DoorPlayModeValidator {
        private const string EnabledKey = "Doors.ValidationOnPlay.Enabled";

        static DoorPlayModeValidator() {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Tools/Doors/Validation On Play/Enabled")]
        private static void ToggleEnabled() {
            var enabled = !IsEnabled();
            EditorPrefs.SetBool(EnabledKey, enabled);
            Menu.SetChecked("Tools/Doors/Validation On Play/Enabled", enabled);
        }

        [MenuItem("Tools/Doors/Validation On Play/Enabled", true)]
        private static bool ToggleEnabledValidate() {
            Menu.SetChecked("Tools/Doors/Validation On Play/Enabled", IsEnabled());
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

            // Print errors and cancel Play.
            for (var i = 0; i < errors.Count; i++) {
                Debug.LogError(errors[i].Message, errors[i].Context);
            }

            EditorApplication.delayCall += () => { EditorApplication.isPlaying = false; };
        }

        private static List<DoorValidator.ValidationError> ValidateOpenScenes() {
            var all = new List<DoorValidator.ValidationError>();

            for (var i = 0; i < EditorSceneManager.sceneCount; i++) {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded) {
                    continue;
                }

                // Если хочешь валидировать только сохранённые сцены — можно тут пропускать scene.isDirty.
                var errors = DoorValidator.ValidateScene(scene);
                all.AddRange(errors);
            }

            return all;
        }
    }
}
#endif
