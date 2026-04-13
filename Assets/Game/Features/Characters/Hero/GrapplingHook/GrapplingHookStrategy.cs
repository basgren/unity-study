using Game.Core.Models.Inventory;
using Game.Defs;
using Game.Features.Characters.Hero.ItemUse;

namespace Game.Features.Characters.Hero.GrapplingHook {
    /// <summary>
    /// Thin adapter that bridges the <see cref="IItemUseStrategy"/> perk interface
    /// to <see cref="GrapplingHookAbility"/>. Contains no logic of its own.
    /// </summary>
    public class GrapplingHookStrategy : IItemUseStrategy {
        public ItemId ItemId => ItemIds.GrapplingHook;

        private readonly PlayerController controller;
        private readonly GrapplingHookAbility ability;

        public GrapplingHookStrategy(PlayerController controller, GrapplingHookAbility ability) {
            this.controller = controller;
            this.ability = ability;
        }

        public bool CanUse() {
            return ability != null
                   && ability.CanActivate()
                   && controller.State.InventoryModel.GetCount(ItemId) > 0;
        }

        public void Use() {
            ability.Activate();
        }

        public void Update(float deltaTime) {
            ability.Tick(deltaTime);
        }
    }
}
