using System;
using Core.Components;
using Core.Services;
using UnityEngine;

namespace Core.Collectables {
    /// <summary>
    /// Collectible component represents an item that can be collected, but which doesn't
    /// occupy space in inventory. For example, coins, health, power-ups, etc.
    /// Object that should receive <c>OnCollected</c> event should implement <c>ICollectableReceiver</c> interface
    /// and <c>collectorTag</c> should have the same value as the tag of the object that received the event.
    /// To use this class, make a derivative class in your game project, specifying the
    /// type of item it represents (enum is recommended).
    /// </summary>
    /// <example><code><![CDATA[
    /// public enum CollectableId { Coin, Health }
    /// 
    /// public class Collectable : CollectableBase<CollectableId> {
    ///   // Class may be empty, but it should inherit from CollectableBase with specific type of item.
    /// }
    /// ]]></code></example>
    /// <seealso cref="ICollectableReceiver{TItemId}"/>
    [RequireComponent(typeof(Collider2D))]
    public class CollectableBase<TItemId> : MonoBehaviour {
        [SerializeField]
        private TItemId itemId;

        /// <summary>
        /// Associated value of the item. For example, silver coin may be 1, while golden - 10.
        /// </summary>
        [SerializeField]
        private float value = 1f;

        /// <summary>
        /// Tag of the collector that can collect this item.
        /// </summary>
        [SerializeField]
        private string collectorTag;

        [SerializeField]
        private SimpleSpriteAnimator pickupAnimationPrefab;

        public event Action OnCollected;
        
        private bool canCollect = true;
        
        /// <summary>
        /// Time after which collectable can be collected again.
        /// </summary>
        private readonly float cannotCollectTime = 1f; 
        private float cannotCollectTimer;

        /// <summary>
        /// Should be called for collectables dropped by a player (collector), so they can't be collected immediately,
        /// and will be collected only after they leave player's trigger area.
        /// </summary>
        public void BlockUntilCollectorExit() {
            canCollect = false;
            cannotCollectTimer = cannotCollectTime;
        }
        
        private void Update() {
            if (cannotCollectTimer > 0) {
                cannotCollectTimer -= Time.deltaTime;

                if (cannotCollectTimer <= 0) {
                    canCollect = true;
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other) {
            TryCollect(other);
        }
        
        private void OnTriggerStay2D(Collider2D other) {
            // For cases when coin drop followed player trajectory and player is still inside trigger area.
            // In this case collection will be allowed by timeout.
            TryCollect(other);
        }

        private void OnTriggerExit2D(Collider2D other) {
            if (!other.CompareTag(collectorTag)) {
                return;
            }

            canCollect = true;
        }
        
        private void TryCollect(Collider2D other) {
            if (!other.CompareTag(collectorTag) || !canCollect) {
                return;
            }

            ICollectableReceiver<TItemId> receiver = other.gameObject.GetComponent<ICollectableReceiver<TItemId>>();

            if (receiver != null) {
                Collect(receiver);
            }
        }

        private void Collect(ICollectableReceiver<TItemId> receiver) {
            receiver.OnCollected(itemId, value);

            if (pickupAnimationPrefab != null) {
                G.Spawner.SpawnVfx(
                    pickupAnimationPrefab,
                    transform.position
                );
            }

            Destroy(gameObject);

            // Call at the very end to apply all effects, as this object may belong to the
            // parent object which could be destroyed in this event.
            OnCollected?.Invoke();
        }
    }
}
