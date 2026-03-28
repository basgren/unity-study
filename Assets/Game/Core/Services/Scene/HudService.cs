using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core.Services.Scene {
    /// <summary>
    /// Automatically loads the HUD scene additively after every gameplay scene load
    /// and skips it for the main menu.
    /// </summary>
    public class HudService : MonoBehaviour {
        private const string HudSceneName = "Hud";
        private const string MainMenuSceneName = "MainMenu";

        public void Init() {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy() {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode) {
            if (scene.name == MainMenuSceneName || scene.name == HudSceneName) {
                return;
            }

            SceneManager.LoadSceneAsync(HudSceneName, LoadSceneMode.Additive);
        }
    }
}