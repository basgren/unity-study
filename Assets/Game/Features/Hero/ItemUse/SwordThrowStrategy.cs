using Game.Core.Bootstrap;
using Game.Core.Models.Inventory;
using Game.Defs;

namespace Game.Features.Characters.Hero.ItemUse {
    public class SwordThrowStrategy : IItemUseStrategy {
        private readonly PlayerController controller;

        public ItemId ItemId => ItemIds.Sword;

        public SwordThrowStrategy(PlayerController controller) {
            this.controller = controller;
        }

        public bool CanUse() {
            return controller.IsArmed
                   && controller.SwordCount > 0;
        }

        public void Use() {
            controller.Animator.SetTrigger(HeroAnimKeys.OnThrowSword);
            G.Audio.Play2D(controller.Sounds.Attack.ThrowSword);
        }

        public void Update(float deltaTime) {
        }
    }
}
