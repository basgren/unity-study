using Game.Core.Models.Inventory;
using Game.Defs;

namespace Game.Features.Characters.Hero.ItemUse {
    public class MediumHealPotionStrategy : SmallHealPotionStrategy {
        public override ItemId ItemId => ItemIds.MediumHealthPotion;
        protected override float HealAmount => 3f;

        public MediumHealPotionStrategy(PlayerController controller) : base(controller) {
        }
    }
}
