using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;

namespace Game.Core.Models.Inventory {
    public class BackpackPanelModel {
        public event Action<IReadOnlyList<InventoryItem>> ItemsUpdated;
        
        public delegate void SelectionChangedHandler(InventoryItem currentItem, InventoryItem prevItem);
        public event SelectionChangedHandler SelectionUpdated; 
        
        private readonly InventoryModel inventoryModel;
        private InventoryItem selectedItem;
        private List<InventoryItem> items = new();

        public IReadOnlyList<InventoryItem> Items => items;
        public bool IsAnyItemSelected => selectedItem != null;
        public int SelectedItemUid => selectedItem?.Uid ?? -1;
        public InventoryItem SelectedItem => selectedItem;
        
        private static readonly ItemType[] HandledItemTypes = { ItemType.Usable, ItemType.Consumable };
        
        public BackpackPanelModel(InventoryModel inventoryModel) {
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
