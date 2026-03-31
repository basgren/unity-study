using System.Collections.Generic;
using Game.Core.Bootstrap;
using Game.Core.Models.Inventory;
using UnityEngine;

namespace Game.UI.Inventory {
    public class SelectedItemPanel : MonoBehaviour {
        [SerializeField]
        private BackpackItemCtrl itemBox;

        private BackpackPanelModel backpack;

        private void Awake() {
            backpack = G.Game.playerState.BackpackPanelModel;
            backpack.ItemsUpdated += OnItemsUpdated;
            backpack.SelectionUpdated += OnSelectionUpdated;
            UpdateItem();
        }

        private void Update() {
            UpdateCooldown();
        }

        private void OnDestroy() {
            backpack.ItemsUpdated -= OnItemsUpdated;
            backpack.SelectionUpdated -= OnSelectionUpdated;
        }

        private void OnSelectionUpdated(InventoryItem currentItem, InventoryItem prevItem) {
            UpdateItem();
        }

        private void OnItemsUpdated(IReadOnlyList<InventoryItem> obj) {
            UpdateItem();
        }

        private void UpdateItem() {
            var currentItem = backpack.SelectedItem;
            itemBox.SetItem(currentItem);
        }

        private void UpdateCooldown() {
            var selected = backpack.SelectedItem;
            var useService = G.Hero.ItemUseService;
            if (selected == null || useService == null) {
                itemBox.SetCooldownProgress(1f);
                return;
            }

            itemBox.SetCooldownProgress(useService.GetCooldownProgress(selected.id));
        }
    }
}
