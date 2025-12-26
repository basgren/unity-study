using System;
using UnityEngine;

namespace Doors {
    /// <summary>
    /// Serializable reference to a scene asset.
    /// Stores the scene GUID (stable across moves/renames) and a cached path for convenience.
    /// </summary>
    [Serializable]
    public struct SceneReference {
        [SerializeField]
        private string sceneGuid;

        [SerializeField]
        private string scenePath;

        /// <summary>
        /// Unity asset GUID of the scene (primary identifier).
        /// </summary>
        public string SceneGuid => sceneGuid;

        /// <summary>
        /// Cached project path (e.g. Assets/Scenes/MyScene.unity). Useful for debugging/editor.
        /// Not used as the primary identifier.
        /// </summary>
        public string ScenePath => scenePath;

        /// <summary>
        /// True if no scene is assigned.
        /// </summary>
        public bool IsEmpty() {
            return string.IsNullOrWhiteSpace(sceneGuid);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Assigns the scene reference from a SceneAsset (Editor-only).
        /// </summary>
        public void EditorSetFromSceneAsset(UnityEditor.SceneAsset asset) {
            if (asset == null) {
                sceneGuid = string.Empty;
                scenePath = string.Empty;
                return;
            }

            scenePath = UnityEditor.AssetDatabase.GetAssetPath(asset);
            sceneGuid = UnityEditor.AssetDatabase.AssetPathToGUID(scenePath);
        }

        /// <summary>
        /// Resolves the referenced scene asset (Editor-only).
        /// </summary>
        public UnityEditor.SceneAsset EditorGetSceneAsset() {
            if (string.IsNullOrWhiteSpace(sceneGuid)) {
                return null;
            }

            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(sceneGuid);
            if (string.IsNullOrWhiteSpace(path)) {
                return null;
            }

            return UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(path);
        }
#endif
    }
}
