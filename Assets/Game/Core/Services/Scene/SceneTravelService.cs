using System;
using System.Collections;
using Game.Core.Bootstrap;
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
        /// <summary>
        /// True while a scene transition (unload + load) is in progress. Lets per-scene components
        /// tell a real teardown from in-scene behavior — e.g. music zones suppress their revert so a
        /// track can continue seamlessly across the load instead of restarting.
        /// </summary>
        public bool IsTransitioning { get; private set; }

        /// <summary>Fires just before the outgoing scene is unloaded. Last chance to read scene objects.</summary>
        public event Action<UnityEngine.SceneManagement.Scene> BeforeUnload;

        /// <summary>Fires after load and PostLoad are complete. (fromScene, toScene)</summary>
        public event Action<UnityEngine.SceneManagement.Scene, UnityEngine.SceneManagement.Scene> AfterTransition;

        /// <summary>Loads a scene by name, firing lifecycle events around the transition.</summary>
        public Coroutine LoadScene(string sceneName, SceneLoadOptions options = default) {
            return StartCoroutine(LoadRoutine(sceneName, SceneManager.GetSceneByName, options));
        }

        /// <summary>
        /// Loads a scene by stable GUID via <see cref="G.SceneCatalog"/>. Use this for scene references
        /// stored in serialized data (door/entrance links, checkpoints) so renames do not break loading.
        /// </summary>
        public Coroutine LoadScene(SceneReference sceneRef, SceneLoadOptions options = default) {
            if (sceneRef.IsEmpty()) {
                Debug.LogError("[SceneTravel] LoadScene called with empty SceneReference.");
                return null;
            }

            var catalog = G.SceneCatalog;
            if (catalog == null) {
                Debug.LogError("[SceneTravel] G.SceneCatalog is not initialized. Check MainConfig.SceneCatalog.");
                return null;
            }

            if (!catalog.TryGetByGuid(sceneRef.SceneGuid, out var resolved) ||
                string.IsNullOrEmpty(resolved.ScenePath)) {
                Debug.LogError(
                    $"[SceneTravel] Scene GUID '{sceneRef.SceneGuid}' (cached path '{sceneRef.ScenePath}') " +
                    "not found in SceneCatalog. Run Tools → Scene State → Rebuild Scene Catalog, and " +
                    "confirm the scene is added to Build Settings (File → Build Settings)."
                );
                return null;
            }

            return StartCoroutine(LoadRoutine(resolved.ScenePath, SceneManager.GetSceneByPath, options));
        }

        /// <summary>Reloads the currently active scene.</summary>
        public Coroutine ReloadActiveScene(SceneLoadOptions options = default) {
            var active = SceneManager.GetActiveScene();
            return LoadScene(active.name, options);
        }

        private IEnumerator LoadRoutine(
            string targetIdentifier,
            Func<string, UnityEngine.SceneManagement.Scene> resolveLoadedScene,
            SceneLoadOptions options
        ) {
            // Held true across the whole unload+load so teardown-time callbacks in the outgoing scene
            // (e.g. a music zone's collider being destroyed) can tell this is a transition, not the
            // player acting. Cleared in finally so an early-out or exception cannot leave it stuck.
            IsTransitioning = true;

            try {
                var fromScene = SceneManager.GetActiveScene();

                if (!options.SkipStateCapture) {
                    BeforeUnload?.Invoke(fromScene);
                }

                var op = SceneManager.LoadSceneAsync(targetIdentifier, LoadSceneMode.Single);
                while (!op.isDone) {
                    yield return null;
                }

                var toScene = resolveLoadedScene(targetIdentifier);

                if (options.PostLoad != null) {
                    yield return options.PostLoad(toScene);
                }

                AfterTransition?.Invoke(fromScene, toScene);
            } finally {
                IsTransitioning = false;
            }
        }
    }
}
