#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Editor.Doors {
    /// <summary>
    /// Shared editor utilities for door management and validation.
    /// </summary>
    public static class DoorEditorUtils {
        /// <summary>
        /// Returns the GUID for the scene at the specified path.
        /// </summary>
        public static string GetSceneGuid(string scenePath) {
            if (string.IsNullOrWhiteSpace(scenePath)) {
                return string.Empty;
            }

            return AssetDatabase.AssetPathToGUID(scenePath);
        }

        /// <summary>
        /// Safely executes an action in a scene. If the scene is not loaded, it will be opened additively,
        /// processed, and then closed.
        /// </summary>
        public static void ExecuteInScene(string scenePath, Action<Scene> action) {
            if (string.IsNullOrWhiteSpace(scenePath)) {
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
