using Core.Utils;
using Game.Core.Bootstrap;
using Game.Core.Models.Inventory;
using Game.Defs;

namespace Game.Features.Characters.Hero.ItemUse {
    public class SwordThrowStrategy : IItemUseStrategy {
        private readonly PlayerController controller;
        private readonly TinyTimer throwCooldown = new TinyTimer(0.5f);

        public ItemId ItemId => ItemIds.Sword;

        public SwordThrowStrategy(PlayerController controller) {
            this.controller = controller;
        }

        public bool CanUse() {
            return controller.IsArmed
                   && throwCooldown.IsTimedOut
                   && controller.SwordCount > 1;
        }

        public void Use() {
            controller.Animator.SetTrigger(HeroAnimKeys.OnThrowSword);
            throwCooldown.Start();
            G.Audio.Play2D(controller.Sounds.Attack.ThrowSword);
        }

        public void Update(float deltaTime) {
            throwCooldown.Update(deltaTime);
        }
    }
}
