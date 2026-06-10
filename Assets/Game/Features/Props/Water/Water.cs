using Core.Components.Extensions;
using Game.Features.Hero;
using UnityEngine;

namespace Game.Features.Props.Water {
    /// <summary>
    /// A water body that swallows characters on contact. It deals no damage: when the hero enters,
    /// it makes the hero bob at the water surface for a moment and then respawn at the last safe
    /// ground position. The surface height is the top edge of this object's trigger collider.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Water : MonoBehaviour {
        [SerializeField, Tooltip("Layers that can be affected by water.")]
        private LayerMask targetLayers;

        private Collider2D myCollider;

        private void Awake() {
            myCollider = GetComponent<Collider2D>();
        }

        private void Reset() {
            myCollider = GetComponent<Collider2D>();
            myCollider.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other) {
            TrySubmerge(other);
        }

        private void TrySubmerge(Collider2D other) {
            if (!targetLayers.Contains(other.gameObject)) {
                return;
            }

            if (!other.TryGetComponent<PlayerController>(out var player)) {
                return;
            }

            // The water surface is the top of the collider that defines the water body's bounds.
            float surfaceY = myCollider.bounds.max.y;
            player.FallIntoWater(surfaceY);
        }
    }
}
