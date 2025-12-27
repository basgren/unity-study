using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace Doors {
    /// <summary>
    /// Shared runtime utilities for door management.
    /// </summary>
    public static class DoorUtils {
        /// <summary>
        /// Retrieves all Door components in the specified scene.
        /// Works both at runtime and in the editor.
        /// </summary>
        public static List<Door> GetDoorsInScene(Scene scene) {
            var result = new List<Door>();
            if (!scene.IsValid()) {
                return result;
            }

            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++) {
                result.AddRange(roots[i].GetComponentsInChildren<Door>(true));
            }

            return result;
        }

        /// <summary>
        /// Finds a door with the specified ID in the given scene.
        /// </summary>
        public static Door FindDoorByIdInScene(Scene scene, string doorId) {
            if (string.IsNullOrEmpty(doorId)) {
                return null;
            }

            var doors = GetDoorsInScene(scene);
            for (var i = 0; i < doors.Count; i++) {
                if (string.Equals(doors[i].DoorId, doorId, StringComparison.Ordinal)) {
                    return doors[i];
                }
            }

            return null;
        }
    }
}
