using Game.Core.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core.Services.Scene {
    /// <summary>
    /// Automatically loads the HUD scene additively after every gameplay scene load
    /// and skips it for the main menu.
    ///
    /// Also exposes <see cref="Hide"/>/<see cref="Show"/> so cutscenes can fade the whole HUD out
    /// while they own the screen and back in when they hand control back. The actual fade lives on
    /// the per-scene <see cref="HudRoot"/>, which registers itself here while it exists; calls are
    /// no-ops when no HUD is present (e.g. the main menu).
    /// </summary>
    public class HudService : MonoBehaviour {
        private const string HudSceneName = "Hud";
        private const string MainMenuSceneName = "MainMenu";

        private HudRoot root;

        // Desired HUD visibility, kept here because the HUD scene loads (and reloads) independently
        // of the callers. Re-applied when a root registers so a Hide/Show requested a frame before
        // the additive HUD scene finished loading is not lost.
        private bool hudVisible = true;

        public void Init() {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy() {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>
        /// Called by the active <see cref="HudRoot"/> when it becomes enabled. Applies the current
        /// desired visibility immediately so a freshly loaded HUD matches the requested state.
        /// </summary>
        public void RegisterRoot(HudRoot newRoot) {
            root = newRoot;
            root.SetVisible(hudVisible, instant: true);
        }

        public void UnregisterRoot(HudRoot oldRoot) {
            if (root == oldRoot) {
                root = null;
            }
        }

        /// <summary>
        /// Fades the HUD out. Intended for cutscenes that take player controls. No-op when no HUD
        /// is loaded.
        /// </summary>
        public void Hide() {
            hudVisible = false;
            if (root != null) {
                root.SetVisible(false, instant: false);
            }
        }

        /// <summary>
        /// Fades the HUD back in. Pair with <see cref="Hide"/> when the cutscene returns controls.
        /// </summary>
        public void Show() {
            hudVisible = true;
            if (root != null) {
                root.SetVisible(true, instant: false);
            }
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode) {
            if (scene.name == MainMenuSceneName || scene.name == HudSceneName) {
                return;
            }

            // A fresh gameplay scene always starts with the HUD visible; a cutscene that wants it
            // hidden calls Hide() during the scene. This also clears any hidden state left over from
            // a previous scene's cutscene (e.g. the ending ship departure that fades to the menu).
            hudVisible = true;

            SceneManager.LoadSceneAsync(HudSceneName, LoadSceneMode.Additive);
        }
    }
}
