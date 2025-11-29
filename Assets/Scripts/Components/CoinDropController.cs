using PixelCrew.Collectibles;
using UnityEngine;

namespace Components {
    /// <summary>
    /// Controller that handles coin drops by the player. The main reason is to prevent coin pickup
    /// at the moment when coins are spawned, as for some time they will collide with the player.
    /// </summary>
    public class CoinDropController : MonoBehaviour {
        private Collectable collectable;
        private Collider2D triggerCollider;

        private void Awake() {
            collectable = GetComponentInChildren<Collectable>();

            if (!collectable) {
                Debug.LogError("CoinDropController requires Collectable component to be present on a child GameObject.",
                    this);
            }

            collectable.BlockUntilCollectorExit();

            collectable.OnCollected += () => {
                Destroy(gameObject);
            };
        }
    }
}
