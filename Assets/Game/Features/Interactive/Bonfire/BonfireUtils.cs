using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace Game.Features.Interactive.Bonfire {
    /// <summary>
    /// Shared runtime utilities for finding Bonfire components in a scene.
    /// </summary>
    public static class BonfireUtils {
        /// <summary>
        /// Retrieves all Bonfire components in the specified scene.
        /// Works both at runtime and in the editor.
        /// </summary>
        public static List<Bonfire> GetBonfiresInScene(Scene scene) {
            var result = new List<Bonfire>();
            if (!scene.IsValid()) {
                return result;
            }

            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++) {
                result.AddRange(roots[i].GetComponentsInChildren<Bonfire>(true));
            }

            return result;
        }

        /// <summary>
        /// Finds a Bonfire with the specified local checkpoint ID in the given scene.
        /// Returns null if not found.
        /// </summary>
        public static Bonfire FindByIdInScene(Scene scene, string localId) {
            if (string.IsNullOrEmpty(localId)) {
                return null;
            }

            var bonfires = GetBonfiresInScene(scene);
            for (var i = 0; i < bonfires.Count; i++) {
                if (string.Equals(bonfires[i].CheckpointId, localId, StringComparison.Ordinal)) {
                    return bonfires[i];
                }
            }

            return null;
        }
    }
}
