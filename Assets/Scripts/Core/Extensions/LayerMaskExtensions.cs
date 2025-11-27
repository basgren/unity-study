using UnityEngine;

namespace Core.Extensions {
    public static class LayerMaskExtensions {
        /// <summary>
        /// Checks if a layer is included in a layer mask.
        /// </summary>
        /// <param name="mask"></param>
        /// <param name="go"></param>
        /// <returns></returns>
        public static bool Contains(this LayerMask mask, GameObject go) {
            return (mask.value & (1 << go.layer)) != 0;
        }
    }
}
