using UnityEngine;

namespace Game.Features.Bosses.StoneGolem.Stones {
    /// <summary>
    /// A stone dropped by the golem's Ground Hit. It falls under its own <see cref="Rigidbody2D"/>
    /// gravity (tune via project gravity / the body's gravity scale) and despawns when it overlaps the
    /// ground (<see cref="groundLayerMask"/>) or after <see cref="maxLifetime"/>, optionally spawning an
    /// impact effect. Player damage is handled by the sibling Damager — this component only owns the
    /// fall cleanup so spent stones don't pile up.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class FallingStone : MonoBehaviour {
        [SerializeField]
        [Tooltip("Layers that stop the stone (ground / floor). On overlap the stone despawns.")]
        private LayerMask groundLayerMask;

        [SerializeField]
        [Tooltip("Optional effect spawned at the stone's position when it lands or expires.")]
        private GameObject impactEffectPrefab;

        [SerializeField]
        [Tooltip("Safety despawn time if the stone never reaches ground.")]
        private float maxLifetime = 6f;

        private float life;
        private bool despawned;

        private void Update() {
            life += Time.deltaTime;
            if (life >= maxLifetime) {
                Despawn();
            }
        }

        private void OnTriggerEnter2D(Collider2D other) {
            // The stone's collider is a trigger, so it passes through the ground visually; detect the
            // overlap here and despawn at the contact point.
            if ((groundLayerMask.value & (1 << other.gameObject.layer)) != 0) {
                Despawn();
            }
        }

        private void Despawn() {
            if (despawned) {
                return;
            }

            despawned = true;
            if (impactEffectPrefab != null) {
                Instantiate(impactEffectPrefab, transform.position, Quaternion.identity);
            }

            Destroy(gameObject);
        }
    }
}
