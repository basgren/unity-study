using Core.Collectables;

namespace PixelCrew.Collectibles {
    public enum CollectableId {
        Coin,
        Health,
        Sword,
    }

    public class Collectable : CollectableBase<CollectableId> {
    }
}
