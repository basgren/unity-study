using System.Collections.Generic;
using Game.Core.Models.Inventory;

namespace Game.Features.Characters.Hero.ItemUse {
    public class ItemUseService {
        private readonly BackpackPanelModel backpackModel;
        private readonly Dictionary<ItemId, IItemUseStrategy> strategies = new();

        public ItemUseService(BackpackPanelModel backpackModel) {
            this.backpackModel = backpackModel;
        }

        public void Register(IItemUseStrategy strategy) {
            strategies[strategy.ItemId] = strategy;
        }

        public void Update(float deltaTime) {
            foreach (var strategy in strategies.Values) {
                strategy.Update(deltaTime);
            }
        }

        public void TryUseSelectedItem() {
            var selected = backpackModel.SelectedItem;
            if (selected == null) {
                return;
            }

            if (strategies.TryGetValue(selected.id, out var strategy) && strategy.CanUse()) {
                strategy.Use();
            }
        }
    }
}
