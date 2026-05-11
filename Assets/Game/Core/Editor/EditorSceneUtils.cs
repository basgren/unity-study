#if UNITY_EDITOR
using System;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Game.Core.Editor {
    /// <summary>
    /// Shared editor utilities for operating on scenes without fully loading them.
    /// </summary>
    public static class EditorSceneUtils {
        /// <summary>
        /// Safely executes an action in a scene. If the scene is not loaded, it will be opened
        /// additively, processed, and then closed.
        /// </summary>
        public static void ExecuteInScene(string scenePath, Action<Scene> action) {
            if (string.IsNullOrWhiteSpace(scenePath)) {
                return;
            }

            // Skip scenes that live outside the project's Assets folder (e.g. inside
            // read-only Unity packages). EditorSceneManager.OpenScene logs an error
            // for those, which would surface during builds as noise even though the
            // validators only care about project-owned scenes.
            if (!scenePath.StartsWith("Assets/")) {
                return;
            }

            var targetScene = SceneManager.GetSceneByPath(scenePath);
            var isAlreadyLoaded = targetScene.IsValid() && targetScene.isLoaded;

            var scene = isAlreadyLoaded
                ? targetScene
                : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            try {
                action(scene);
            }
            finally {
                if (!isAlreadyLoaded) {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }
}
#endif