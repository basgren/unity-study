using System.Collections.Generic;
using UnityEngine;

namespace Utils {
    public static class Geometry {
        public static T FindClosest<T>(IEnumerable<T> items, Vector3 origin)
            where T : MonoBehaviour {
            T closest = null;
            float closestSqr = float.PositiveInfinity;

            foreach (T item in items) {
                if (item == null) {
                    continue;
                }

                float sqr = (item.transform.position - origin).sqrMagnitude;

                if (sqr < closestSqr) {
                    closestSqr = sqr;
                    closest = item;
                }
            }

            return closest;
        }
    }
}
