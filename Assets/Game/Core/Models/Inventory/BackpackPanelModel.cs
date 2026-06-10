using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;

namespace Game.Core.Models.Inventory {
    public class BackpackPanelModel : ISelectablePanelModel {
        public event Action<IReadOnlyList<InventoryItem>> ItemsUpdated;
        public event SelectionChangedHandler SelectionUpdated;
        
        private readonly InventoryModel inventoryModel;
        private InventoryItem selectedItem;
        private List<InventoryItem> items = new();

        public IReadOnlyList<InventoryItem> Items => items;
        public bool IsAnyItemSelected => selectedItem != null;
        public int SelectedItemUid => selectedItem?.Uid ?? -1;
        public InventoryItem SelectedItem => selectedItem;
        
        // Item types shown in the backpack panel. Keys are displayed for reference but cannot be selected.
        private static readonly ItemType[] DisplayedItemTypes = { ItemType.Usable, ItemType.Consumable, ItemType.Key };

        // Subset of displayed types the selection cursor can land on. Keys are intentionally excluded.
        private static readonly ItemType[] SelectableItemTypes = { ItemType.Usable, ItemType.Consumable };

        public BackpackPanelModel(InventoryModel inventoryModel) {
            this.inventoryModel = inventoryModel;
            inventoryModel.OnChange += OnInventoryChange;
            Update();
        }

        private void OnInventoryChange(InventoryChangeEvent eventInfo) {
            var itemDef = DefsFacade.I.Items.Get(eventInfo.ItemId);

            if (DisplayedItemTypes.Contains(itemDef.Type)) {
                Update();
            }
        }

        private void Update() {
            items = inventoryModel.GetAll(DisplayedItemTypes);

            if (IsAnyItemSelected && items.All(i => i.Uid != selectedItem.Uid)) {
                selectedItem = null;
            }

            // Only land the selection on a selectable item; keys stay visible but never get auto-selected.
            if (!IsAnyItemSelected) {
                selectedItem = FirstSelectable();
            }

            ItemsUpdated?.Invoke(items);
        }

        public void NextItem() {
            var selectable = GetSelectableItems();
            if (selectable.Count == 0) {
                return;
            }

            var prevSelection = selectedItem;
            var index = selectable.IndexOf(selectedItem);

            selectedItem = selectable[(index + 1) % selectable.Count];
            SelectionUpdated?.Invoke(selectedItem, prevSelection);
        }

        public void PrevItem() {
            var selectable = GetSelectableItems();
            if (selectable.Count == 0) {
                return;
            }

            var prevSelection = selectedItem;
            var index = selectable.IndexOf(selectedItem);
            if (index < 0) {
                index = 0;
            }

            selectedItem = selectable[(selectable.Count + index - 1) % selectable.Count];
            SelectionUpdated?.Invoke(selectedItem, prevSelection);
        }

        private InventoryItem FirstSelectable() {
            foreach (var item in items) {
                if (IsSelectable(item)) {
                    return item;
                }
            }

            return null;
        }

        private List<InventoryItem> GetSelectableItems() {
            var result = new List<InventoryItem>();
            foreach (var item in items) {
                if (IsSelectable(item)) {
                    result.Add(item);
                }
            }

            return result;
        }

        private static bool IsSelectable(InventoryItem item) {
            var type = DefsFacade.I.Items.Get(item.id).Type;
            return SelectableItemTypes.Contains(type);
        }
    }
}
