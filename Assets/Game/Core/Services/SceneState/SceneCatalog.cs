using System.Collections.Generic;
using Game.Core.Services.Scene;
using UnityEngine;

namespace Game.Core.Services.SceneState {
    /// <summary>
    /// ScriptableObject that maps scene paths to stable GUIDs so that saved state keys survive
    /// scene renames and moves. Populated at edit time by the SceneCatalogBuilder tool and
    /// shipped as part of the game data.
    /// </summary>
    [CreateAssetMenu(menuName = "Config/Scene Catalog", fileName = "SceneCatalog")]
    public sealed class SceneCatalog : ScriptableObject {
        [SerializeField]
        private SceneReference[] scenes;

        private Dictionary<string, SceneReference> byPath;
        private Dictionary<string, SceneReference> byGuid;
        private HashSet<string> warnedPaths;

        /// <summary>Rebuilds the internal lookup dictionaries from the serialized array.</summary>
        public void RebuildIndex() {
            int capacity = scenes != null ? scenes.Length : 0;
            byPath = new Dictionary<string, SceneReference>(capacity);
            byGuid = new Dictionary<string, SceneReference>(capacity);
            warnedPaths = new HashSet<string>();

            if (scenes == null) {
                return;
            }

            foreach (var s in scenes) {
                if (!string.IsNullOrEmpty(s.ScenePath)) {
                    byPath[s.ScenePath] = s;
                }

                if (!string.IsNullOrEmpty(s.SceneGuid)) {
                    byGuid[s.SceneGuid] = s;
                }
            }
        }

        /// <summary>
        /// Returns the GUID for the given runtime scene, or an empty string if not in the catalog.
        /// Logs a warning once per unknown scene path.
        /// </summary>
        public string GuidForScene(UnityEngine.SceneManagement.Scene scene) {
            EnsureIndex();

            if (string.IsNullOrEmpty(scene.path)) {
                return string.Empty;
            }

            if (byPath.TryGetValue(scene.path, out var sceneRef)) {
                return sceneRef.SceneGuid;
            }

            if (warnedPaths.Add(scene.path)) {
                Debug.LogWarning(
                    $"[SceneCatalog] Scene '{scene.path}' is not in the catalog. " +
                    "State capture/restore will be skipped. " +
                    "Run: Tools \u2192 Scene State \u2192 Rebuild Scene Catalog"
                );
            }

            return string.Empty;
        }

        /// <summary>Resolves a runtime scene to its catalog entry.</summary>
        public bool TryResolve(UnityEngine.SceneManagement.Scene runtimeScene, out SceneReference result) {
            EnsureIndex();

            if (string.IsNullOrEmpty(runtimeScene.path)) {
                result = default;
                return false;
            }

            return byPath.TryGetValue(runtimeScene.path, out result);
        }

        /// <summary>Finds a catalog entry by its scene GUID.</summary>
        public bool TryGetByGuid(string guid, out SceneReference result) {
            EnsureIndex();

            if (string.IsNullOrEmpty(guid)) {
                result = default;
                return false;
            }

            return byGuid.TryGetValue(guid, out result);
        }

        private void EnsureIndex() {
            if (byPath == null) {
                RebuildIndex();
            }
        }
    }
}
