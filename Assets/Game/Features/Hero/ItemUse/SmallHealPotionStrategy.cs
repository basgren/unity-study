using Game.Core.Models.Inventory;
using Game.Defs;
using Game.Features.Hero;

namespace Game.Features.Characters.Hero.ItemUse {
    public class SmallHealPotionStrategy : IItemUseStrategy {
        public virtual ItemId ItemId => ItemIds.SmallHealthPotion;
        protected virtual float HealAmount => 1f;
        private readonly PlayerController controller;

        public SmallHealPotionStrategy(PlayerController controller) {
            this.controller = controller;
        }
        
        public bool CanUse() {
            return controller.State.InventoryModel.GetCount(ItemId) > 0;
        }

        public void Use() {
            controller.Damageable.AddHealth(HealAmount);
            controller.State.InventoryModel.Remove(ItemId, 1);
        }

        public void Update(float deltaTime) {
        }
    }
}
