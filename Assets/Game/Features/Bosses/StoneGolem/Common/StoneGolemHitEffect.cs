using Game.Core.Bootstrap;
using Game.Core.Components.Damage;
using UnityEngine;

namespace Game.Features.Bosses.StoneGolem.Common {
    /// <summary>
    /// Ad-hoc hit feedback: spawns a one-shot effect prefab at the approximate contact point
    /// when the golem takes damage. Wired in the prefab to Damageable.onHit (dynamic Damager call).
    /// </summary>
    public class StoneGolemHitEffect : MonoBehaviour {
        // Rejection-sampling budget for picking a point inside both colliders. The AABB overlap
        // of two box-ish colliders is mostly filled, so a handful of tries almost always lands.
        private const int MaxSampleAttempts = 10;

        [SerializeField]
        [Tooltip("One-shot effect spawned at the contact point. The prefab must destroy itself when finished.")]
        private GameObject effectPrefab;

        private Collider2D[] bodyColliders;

        private void Awake() {
            // The golem swaps between the normal and the ball collider, so cache all of them and
            // resolve the enabled one per hit.
            bodyColliders = GetComponents<Collider2D>();
        }

        public void OnHit(Damager damager) {
            if (effectPrefab == null || damager == null || damager.DamageCollider == null) {
                return;
            }

            G.Spawner.SpawnVfx(effectPrefab, FindSpawnPoint(damager.DamageCollider));
        }

        /// <summary>
        /// Picks a random point inside the intersection of the golem body and the damager collider,
        /// so repeated hits spark in slightly different spots. Trigger contacts expose no real
        /// contact point, so the area is sampled: random points in the AABB overlap are tested
        /// against both actual shapes. Falls back to the closest point on the damager's collider
        /// to the golem center when sampling fails (degenerate sliver of overlap).
        /// </summary>
        private Vector2 FindSpawnPoint(Collider2D damagerCollider) {
            Collider2D body = GetActiveBodyCollider();
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

            // The damager's collider is used for the fallback (not the golem's own) because it is
            // guaranteed enabled during the hit and so can answer ClosestPoint queries.
            return damagerCollider.ClosestPoint(transform.position);
        }

        private Collider2D GetActiveBodyCollider() {
            foreach (Collider2D col in bodyColliders) {
                if (col.enabled && !col.isTrigger) {
                    return col;
                }
            }

            return null;
        }
    }
}
