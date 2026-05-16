using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace Game.Features.Doors {
    /// <summary>
    /// Shared runtime utilities for entrance management.
    /// </summary>
    public static class EntranceUtils {
        /// <summary>
        /// Retrieves all Entrance components in the specified scene.
        /// Works both at runtime and in the editor.
        /// </summary>
        public static List<Entrance> GetEntrancesInScene(Scene scene) {
            var result = new List<Entrance>();
            if (!scene.IsValid()) {
                return result;
            }

            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++) {
                result.AddRange(roots[i].GetComponentsInChildren<Entrance>(true));
            }

            return result;
        }

        /// <summary>
        /// Finds an entrance with the specified ID in the given scene.
        /// </summary>
        public static Entrance FindEntranceByIdInScene(Scene scene, string entranceId) {
            if (string.IsNullOrEmpty(entranceId)) {
                return null;
            }

            var entrances = GetEntrancesInScene(scene);
            for (var i = 0; i < entrances.Count; i++) {
                if (string.Equals(entrances[i].EntranceId, entranceId, StringComparison.Ordinal)) {
                    return entrances[i];
                }
            }

            return null;
        }
    }
}
