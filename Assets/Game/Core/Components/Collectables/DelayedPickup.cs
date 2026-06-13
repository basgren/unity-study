using Game.Core.Utils;
using UnityEngine;

namespace Game.Core.Components.Collectables {
    /// <summary>
    /// Controller that handles coin drops by the player. The main reason is to prevent coin pickup
    /// at the moment when coins are spawned, as for some time they will collide with the player.
    /// </summary>
    public class DelayedPickup : MonoBehaviour {
        private Collectable collectable;
        private Collider2D triggerCollider;

        /// <summary>
        /// Time after which the dropped collectable can be collected.
        /// </summary>
        private readonly TinyTimer cannotCollectTimer = new TinyTimer(0.5f);
        
        private void Awake() {
            collectable = GetComponentInChildren<Collectable>();

            if (!collectable) {
                Debug.LogError("CoinDropController requires Collectable component to be present on a child GameObject.",
                    this);
            }

            collectable.BlockUntilCollectorExit();

            cannotCollectTimer.OnTimeout += () => {
                collectable.AllowCollection();
            };
            
            collectable.OnCollected += () => {
                Destroy(gameObject);
            };
            
            cannotCollectTimer.Start();
        }

        private void Update() {
            cannotCollectTimer.Update(Time.deltaTime);
        }
    }
}
