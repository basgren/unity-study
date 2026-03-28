using System.Collections.Generic;
using Game.Core.Bootstrap;
using Game.Core.Models.Inventory;
using UnityEngine;

namespace Game.UI.Inventory {
    public class SelectedItemPanel : MonoBehaviour {
        [SerializeField]
        private BackpackItemCtrl itemBox;
        
        private BackpackPanelModel backpack;
        private InventoryItem item;

        private void Awake() {
            backpack = G.Game.playerState.BackpackPanelModel;
            backpack.ItemsUpdated += OnItemsUpdated;
            backpack.SelectionUpdated += OnSelectionUpdated;
            UpdateItem();
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
    }
}
