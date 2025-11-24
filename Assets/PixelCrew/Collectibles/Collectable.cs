using Core.Collectables;

namespace PixelCrew.Collectibles {
    public enum CollectableId {
        Coin,
        Health,
    }

    public class Collectable : CollectableBase<CollectableId> {
    }
}
