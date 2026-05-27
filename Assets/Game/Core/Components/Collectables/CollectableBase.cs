using System;
using Core.Audio;
using Game.Core.Bootstrap;
using Game.Core.Components.Animation;
using Game.Core.Services.SceneState;
using Game.Core.Services.SceneState.Savers;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Core.Components.Collectables {
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
        
        [SerializeField]
        private AudioCue collectSound;

        /// <summary>
        /// When collectable is a child object, specify root object of collectable, so it can be destroyed.
        /// </summary>
        [SerializeField]
        private GameObject rootObject;

        public event Action OnCollected;

        /// <summary>
        /// Inspector-wirable equivalent of <see cref="OnCollected"/>, raised when the item is collected.
        /// Use it to trigger scene reactions on pickup (e.g. opening a door) without writing code.
        /// </summary>
        [SerializeField]
        private UnityEvent onCollected;

        private bool canCollect = true;

        /// <summary>
        /// Should be called for collectables dropped by a player (collector), so they can't be collected immediately,
        /// and will be collected only after they leave player's trigger area or on timeout.
        /// </summary>
        public void BlockUntilCollectorExit() {
            canCollect = false;
        }

        public void AllowCollection() {
            canCollect = true;
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
            if (canCollect || !other.CompareTag(collectorTag)) {
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

            if (collectSound != null) {
                G.Audio.PlayAt(collectSound, transform.position);
            }

            var existenceSaver = GetComponentInParent<DestructionStateSaver>();

            if (existenceSaver != null) {
                existenceSaver.MarkDestroyed();
            }
            
            if (rootObject != null) {
                Destroy(rootObject);
            } else {
                Destroy(gameObject);                
            }

            // Call at the very end to apply all effects, as this object may belong to the
            // parent object which could be destroyed in this event. (Destroy is deferred to end of
            // frame, so this object still exists and the subscribers / UnityEvent targets fire.)
            OnCollected?.Invoke();
            onCollected?.Invoke();
        }
    }
}
