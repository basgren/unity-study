using Game.Core.Bootstrap;
using Game.Core.Components.Damage;
using UnityEngine;

namespace Game.Features.Bosses.StoneGolem.Common {
    /// <summary>
    /// Ad-hoc hit feedback: spawns a one-shot effect prefab at the approximate contact point
    /// when the golem takes damage. Wired in the prefab to Damageable.onHit (dynamic Damager call).
    /// </summary>
    public class StoneGolemHitEffect : MonoBehaviour {
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

            Collider2D body = GetActiveBodyCollider();
            Vector2 point = HitEffectPlacement.FindSpawnPoint(body, damager.DamageCollider, transform.position);
            G.Spawner.SpawnVfx(effectPrefab, point);
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
