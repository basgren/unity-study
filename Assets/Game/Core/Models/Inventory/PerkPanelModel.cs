using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;

namespace Game.Core.Models.Inventory {
    /// <summary>
    /// Tracks perk-type items in the inventory and manages selection.
    /// Mirrors <see cref="BackpackPanelModel"/> but filters for <see cref="ItemType.Perk"/>.
    /// </summary>
    public class PerkPanelModel : ISelectablePanelModel {
        public event Action<IReadOnlyList<InventoryItem>> ItemsUpdated;
        public event SelectionChangedHandler SelectionUpdated;

        private readonly InventoryModel inventoryModel;
        private InventoryItem selectedItem;
        private List<InventoryItem> items = new();

        public IReadOnlyList<InventoryItem> Items => items;
        public bool IsAnyItemSelected => selectedItem != null;
        public int SelectedItemUid => selectedItem?.Uid ?? -1;
        public InventoryItem SelectedItem => selectedItem;

        private static readonly ItemType[] HandledItemTypes = { ItemType.Perk };

        public PerkPanelModel(InventoryModel inventoryModel) {
            this.inventoryModel = inventoryModel;
            inventoryModel.OnChange += OnInventoryChange;
            Update();
        }

        private void OnInventoryChange(InventoryChangeEvent eventInfo) {
            var itemDef = DefsFacade.I.Items.Get(eventInfo.ItemId);

            if (HandledItemTypes.Contains(itemDef.Type)) {
                Update();
            }
        }

        private void Update() {
            items = inventoryModel.GetAll(HandledItemTypes);

            if (IsAnyItemSelected && items.All(i => i.Uid != selectedItem.Uid)) {
                selectedItem = null;
            }

            if (items.Count > 0) {
                if (!IsAnyItemSelected) {
                    selectedItem = items[0];
                }
            } else {
                selectedItem = null;
            }

            ItemsUpdated?.Invoke(items);
        }

        public void NextItem() {
            if (items.Count == 0) {
                return;
            }

            var prevSelection = selectedItem;
            var index = items.IndexOf(selectedItem);

            selectedItem = items[(index + 1) % items.Count];
            SelectionUpdated?.Invoke(selectedItem, prevSelection);
        }

        public void PrevItem() {
            if (items.Count == 0) {
                return;
            }

            var prevSelection = selectedItem;
            var index = items.IndexOf(selectedItem);

            selectedItem = items[(items.Count + index - 1) % items.Count];
            SelectionUpdated?.Invoke(selectedItem, prevSelection);
        }
    }
}
