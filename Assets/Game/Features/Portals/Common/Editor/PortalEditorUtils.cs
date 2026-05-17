#if UNITY_EDITOR
using System;
using Game.Core.Editor;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace Game.Features.Portals.Common.Editor {
    /// <summary>
    /// Shared editor utilities for portal management and validation (Doors, Entrances, ...).
    /// </summary>
    public static class PortalEditorUtils {
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
        /// Safely executes an action in a scene. Forwards to the shared EditorSceneUtils.
        /// </summary>
        public static void ExecuteInScene(string scenePath, Action<Scene> action) {
            EditorSceneUtils.ExecuteInScene(scenePath, action);
        }
    }
}
#endif
