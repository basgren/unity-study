using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Features.Doors {
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
        /// True if no scene is assigned. Accepts either a GUID or a path as evidence of a valid reference.
        /// </summary>
        public bool IsEmpty() {
            return string.IsNullOrWhiteSpace(sceneGuid) && string.IsNullOrWhiteSpace(scenePath);
        }

        public string GetSceneName() {
            if (string.IsNullOrWhiteSpace(scenePath)) {
                return string.Empty;
            }

            return Path.GetFileNameWithoutExtension(scenePath);
        }

        /// <summary>
        /// Constructs a SceneReference from a runtime scene.
        /// GUID is not available at runtime, so only the path is stored.
        /// </summary>
        public static SceneReference FromScene(Scene scene) {
            return new SceneReference {
                sceneGuid = string.Empty,
                scenePath = scene.path,
            };
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
