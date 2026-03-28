using Game.Core.Models.Inventory;

namespace Game.Features.Characters.Hero.ItemUse {
    public interface IItemUseStrategy {
        ItemId ItemId { get; }
        bool CanUse();
        void Use();
        void Update(float deltaTime);
    }
}
