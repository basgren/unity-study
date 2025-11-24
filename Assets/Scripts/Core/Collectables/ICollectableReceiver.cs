namespace Core.Collectables {
    /// <summary>
    /// Implement this interface in the component that should receive collectable events.
    /// </summary>
    /// <example><code><![CDATA[
    /// public class PlayerController: MonoBehaviour, ICollectableReceiver<CollectableId> {
    ///   ...
    ///   OnCollected(CollectableId itemId, float value) {
    ///     switch (itemId) {
    ///       case CollectableId.Coin: coins += value;
    ///       case CollectableId.Health: health += value;
    ///     }
    ///   }
    /// }
    /// ]]></code></example>
    /// <typeparam name="TItemId">Type to be used to identify collectable.</typeparam>
    /// <seealso cref="CollectableBase{TItemId}"/>
    public interface ICollectableReceiver<in TItemId> {
        void OnCollected(TItemId itemId, float value);
    }
}
