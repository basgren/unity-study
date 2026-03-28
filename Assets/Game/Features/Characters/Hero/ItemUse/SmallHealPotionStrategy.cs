using Game.Core.Models.Inventory;
using Game.Defs;

namespace Game.Features.Characters.Hero.ItemUse {
    public class SmallHealPotionStrategy : IItemUseStrategy {
        public virtual ItemId ItemId => ItemIds.SmallHealthPotion;
        protected virtual float HealAmount => 1f;
        private readonly PlayerController controller;

        public SmallHealPotionStrategy(PlayerController controller) {
            this.controller = controller;
        }
        
        public bool CanUse() {
            return controller.State.Inventory.GetCount(ItemId) > 0;
        }

        public void Use() {
            controller.Damageable.AddHealth(HealAmount);
            controller.State.Inventory.Remove(ItemId, 1);
        }

        public void Update(float deltaTime) {
        }
    }
}
