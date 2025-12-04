using System;
using System.Collections.Generic;
using UnityEngine;

namespace Utils {
    public enum Direction2D {
        Up,
        Down,
        Left,
        Right
    }

    public static class Geometry {
        public static Vector2 GetDirVector(Direction2D dir) {
            switch (dir) {
                case Direction2D.Up: return Vector2.up;
                case Direction2D.Down: return Vector2.down;
                case Direction2D.Left: return Vector2.left;
                default: return Vector2.right;
            }
        }

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

        /// <summary>
        /// Tries to detect a small "step" in front of the origin by casting short horizontal rays.
        /// Starts from the given origin at foot level and moves the origin up in pixel-sized increments
        /// until it either finds a clear ray (no collision) or exceeds the maximum step height.
        /// </summary>
        /// <param name="origin">
        /// Starting point for the raycasts (usually somewhere near the bottom of the collider).
        /// </param>
        /// <param name="direction">
        /// Horizontal direction to cast rays. Only left/right are expected; the X sign is used.
        /// </param>
        /// <param name="maxStepHeight">
        /// Maximum allowed step height in world units (same units as the origin).
        /// </param>
        /// <param name="raycastIncrement">
        /// Vertical increment step in world units (size of one "pixel" or minimal Y step).
        /// </param>
        /// <param name="rayLength">Length of ray in front of player</param>
        /// <param name="layerMask">
        /// Layer mask to use for raycasts.
        /// </param>
        /// <param name="stepHeight">
        /// Output step height in world units at which the ray becomes free (no collision).
        /// Meaningful only if the method returns true.
        /// </param>
        /// <returns>
        /// True if a valid step height was found (there is an obstacle at the base, but a clear ray
        /// was found within maxStepHeight). False if there is no obstacle at the base level or if
        /// all rays up to maxStepHeight still hit something.
        /// </returns>
        public static bool TryFindStepHeight(
            Vector2 origin,
            Vector2 direction,
            float maxStepHeight,
            float raycastIncrement,
            float rayLength,
            LayerMask layerMask,
            out float stepHeight
        ) {
            stepHeight = 0f;

            if (raycastIncrement <= 0f || maxStepHeight <= 0f) {
                return false;
            }

            // Use only X, we don't need Y here
            float dirSign = Mathf.Sign(direction.x);
            if (Mathf.Approximately(dirSign, 0f)) {
                return false; // no horizontal movement
            }

            Vector2 rayDir = new Vector2(dirSign, 0f);

            int maxSteps = Mathf.CeilToInt(maxStepHeight / raycastIncrement);
            if (maxSteps <= 0) {
                return false;
            }

            RaycastHit2D hit;

            for (int i = 0; i <= maxSteps; i++) {
                float offsetY = i * raycastIncrement;
                Vector2 currentOrigin = origin + Vector2.up * offsetY;

                hit = Physics2D.Raycast(currentOrigin, rayDir, rayLength, layerMask);
                // Debug.DrawRay(currentOrigin, rayDir * rayLength, Color.red);

                bool hasHit = hit.collider != null;

                if (!hasHit) {
                    stepHeight = offsetY;
                    // For the first check return false even if there are no hits, as this means that
                    // there's no step and no actions needed.
                    return i != 0;
                }
            }

            stepHeight = 0f;
            return false;
        }
    }
}
