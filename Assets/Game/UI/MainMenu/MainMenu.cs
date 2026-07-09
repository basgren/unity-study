using Game.Core.Bootstrap;
using Game.Core.Services.Scene;
using Game.Core.UI;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace Game.UI.MainMenu {
    public class MainMenu : AnimatedWindow {
        [SerializeField]
        private SceneReference startScene;

        [SerializeField]
        [Tooltip("Shown only when a saved resting point exists. Resumes from the last checkpoint.")]
        private GameObject continueButton;

        // Localization key for the New Game confirmation prompt (UI string table).
        private const string UiTableName = "UI";
        private const string NewGameConfirmKey = "ui.main_menu.confirm_new_game";

        private void OnEnable() {
            RefreshContinueButton();
        }

        private void RefreshContinueButton() {
            if (continueButton != null) {
                continueButton.SetActive(HasSavedGame());
            }
        }

        private static bool HasSavedGame() {
            return G.Checkpoint != null && G.Checkpoint.Current.HasValue;
        }

        public void OnContinueClick() {
            if (!HasSavedGame()) {
                return;
            }

            // Mirror the cross-scene death-respawn path: flag a pending respawn, then load the
            // checkpoint's scene. The loaded PlayerController.Start teleports the hero to the
            // bonfire spawn and restores health; StateRoot.Start re-applies world state.
            var checkpointScene = G.Checkpoint.Current.Value.Scene.GetSceneName();
            G.Checkpoint.RequestRespawn();
            SceneTransition.FadeToScene(checkpointScene);
        }

        public void OnNewGameClick() {
            if (HasSavedGame()) {
                var dialog = G.Menu.OpenMenu(G.Config.ConfirmDialog) as ConfirmDialog.ConfirmDialog;
                if (dialog != null) {
                    dialog.Configure(GetNewGameConfirmMessage(), ResetAndStart);
                    return;
                }

                // Fallback: if the confirm dialog is not wired, start anyway rather than dead-end.
                Debug.LogWarning("MainMenu: ConfirmDialog is not assigned in MainConfig; starting new game without confirmation.");
            }

            ResetAndStart();
        }

        // Resolves the localized New Game confirmation prompt, falling back to the key if missing.
        private static string GetNewGameConfirmMessage() {
            var localized = LocalizationSettings.StringDatabase.GetLocalizedString(UiTableName, NewGameConfirmKey);
            return string.IsNullOrEmpty(localized) ? NewGameConfirmKey : localized;
        }

        private void ResetAndStart() {
            // SkipStateCapture: starting fresh from the main menu, nothing to preserve. The reset
            // runs while the screen is fully faded, just before menus close and the scene loads.
            SceneTransition.FadeToScene(
                startScene.GetSceneName(),
                new SceneLoadOptions { SkipStateCapture = true },
                () => {
                    G.Game.ResetPlayerState();
                    G.Checkpoint.Reset();
                    G.SceneState.ResetAll();
                    G.Save.DeleteSave();
                }
            );
        }

        public void OnOptionsClick() {
            G.Menu.OpenOptionsMenu();
        }

        public void OnExitClick() {
            G.Menu.CloseAll();
            Application.Quit();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
