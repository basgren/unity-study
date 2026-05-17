using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Features.Portals.Common {
    /// <summary>
    /// Shared runtime helpers for collecting portals of a given kind from a scene.
    /// </summary>
    public static class PortalUtils {
        /// <summary>
        /// Returns all components of type <typeparamref name="T"/> in the scene, walking every root.
        /// </summary>
        public static List<T> GetPortalsInScene<T>(Scene scene) where T : Component {
            var result = new List<T>();
            if (!scene.IsValid()) {
                return result;
            }

            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++) {
                result.AddRange(roots[i].GetComponentsInChildren<T>(true));
            }

            return result;
        }

        /// <summary>
        /// Finds the portal of kind <typeparamref name="T"/> whose <see cref="IPortal.Id"/> matches.
        /// Returns null when not found.
        /// </summary>
        public static T FindPortalByIdInScene<T>(Scene scene, string id) where T : Component, IPortal {
            if (string.IsNullOrEmpty(id) || !scene.IsValid()) {
                return null;
            }

            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++) {
                var portals = roots[i].GetComponentsInChildren<T>(true);
                for (var j = 0; j < portals.Length; j++) {
                    IPortal p = portals[j];
                    if (string.Equals(p.Id, id, StringComparison.Ordinal)) {
                        return portals[j];
                    }
                }
            }

            return null;
        }
    }
}
