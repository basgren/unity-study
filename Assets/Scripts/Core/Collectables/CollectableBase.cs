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

        private void OnTriggerEnter2D(Collider2D other) {
            if (!other.CompareTag(collectorTag)) {
                return;
            }

            ICollectableReceiver<TItemId> receiver = other.gameObject.GetComponent<ICollectableReceiver<TItemId>>();

            if (receiver != null) {
                OnCollected(receiver);
            }
        }

        protected virtual void OnCollected(ICollectableReceiver<TItemId> receiver) {
            receiver.OnCollected(itemId, value);
            Destroy(gameObject);
        }
    }
}
