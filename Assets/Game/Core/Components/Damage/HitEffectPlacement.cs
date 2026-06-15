using UnityEngine;

namespace Game.Core.Components.Damage {
    /// <summary>
    /// Shared placement logic for one-shot hit effects. Picks a random point inside the
    /// intersection of the hit body and the damager collider so repeated hits spark in
    /// slightly different spots.
    /// </summary>
    public static class HitEffectPlacement {
        // Rejection-sampling budget for picking a point inside both colliders. The AABB overlap
        // of two box-ish colliders is mostly filled, so a handful of tries almost always lands.
        private const int MaxSampleAttempts = 10;

        /// <summary>
        /// Picks a random point inside the overlap of <paramref name="body"/> and
        /// <paramref name="damagerCollider"/>. Trigger contacts expose no real contact point, so
        /// the AABB overlap is sampled and tested against both actual shapes. Falls back to the
        /// closest point on the damager's collider to <paramref name="origin"/> when sampling
        /// fails (degenerate sliver of overlap) or no body collider is available.
        /// </summary>
        public static Vector2 FindSpawnPoint(Collider2D body, Collider2D damagerCollider, Vector2 origin) {
            if (body != null) {
                Vector2 min = Vector2.Max(body.bounds.min, damagerCollider.bounds.min);
                Vector2 max = Vector2.Min(body.bounds.max, damagerCollider.bounds.max);

                if (min.x <= max.x && min.y <= max.y) {
                    for (int i = 0; i < MaxSampleAttempts; i++) {
                        Vector2 p = new Vector2(Random.Range(min.x, max.x), Random.Range(min.y, max.y));
                        if (body.OverlapPoint(p) && damagerCollider.OverlapPoint(p)) {
                            return p;
                        }
                    }
                }
            }

            // The damager's collider is used for the fallback (not the body's own) because it is
            // guaranteed enabled during the hit and so can answer ClosestPoint queries.
            return damagerCollider.ClosestPoint(origin);
        }
    }
}
