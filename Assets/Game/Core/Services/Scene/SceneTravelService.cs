using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core.Services.Scene {
    /// <summary>
    /// Options for a scene load request.
    /// </summary>
    public struct SceneLoadOptions {
        /// <summary>
        /// When true, the BeforeUnload event is not fired and state capture is skipped.
        /// Use for transitions where no gameplay state should be preserved (e.g. returning to main menu).
        /// </summary>
        public bool SkipStateCapture;

        /// <summary>
        /// Optional coroutine that runs after the new scene finishes loading, before AfterTransition fires.
        /// Use to perform actions that require the new scene to be fully loaded (e.g. teleporting the player).
        /// </summary>
        public Func<UnityEngine.SceneManagement.Scene, IEnumerator> PostLoad;
    }

    /// <summary>
    /// Centralized scene loader. All non-additive scene transitions must go through this service
    /// so that BeforeUnload listeners (e.g. SceneStateService) can capture state before the scene
    /// is destroyed.
    /// </summary>
    public class SceneTravelService : MonoBehaviour {
        /// <summary>Fires just before the outgoing scene is unloaded. Last chance to read scene objects.</summary>
        public event Action<UnityEngine.SceneManagement.Scene> BeforeUnload;

        /// <summary>Fires after load and PostLoad are complete. (fromScene, toScene)</summary>
        public event Action<UnityEngine.SceneManagement.Scene, UnityEngine.SceneManagement.Scene> AfterTransition;

        /// <summary>Loads a scene by name, firing lifecycle events around the transition.</summary>
        public Coroutine LoadScene(string sceneName, SceneLoadOptions options = default) {
            return StartCoroutine(LoadRoutine(sceneName, options));
        }

        /// <summary>Reloads the currently active scene.</summary>
        public Coroutine ReloadActiveScene(SceneLoadOptions options = default) {
            var active = SceneManager.GetActiveScene();
            return LoadScene(active.name, options);
        }

        private IEnumerator LoadRoutine(string targetSceneName, SceneLoadOptions options) {
            var fromScene = SceneManager.GetActiveScene();

            if (!options.SkipStateCapture) {
                BeforeUnload?.Invoke(fromScene);
            }

            var op = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
            while (!op.isDone) {
                yield return null;
            }

            var toScene = SceneManager.GetSceneByName(targetSceneName);

            if (options.PostLoad != null) {
                yield return options.PostLoad(toScene);
            }

            AfterTransition?.Invoke(fromScene, toScene);
        }
    }
}
