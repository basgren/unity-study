using System;
using System.Collections.Generic;
using Game.Models;
using UnityEngine;

namespace Core.Models.Inventory {
    [Serializable]
    public class Inventory {
        [SerializeField]
        private List<InventoryItem> items = new List<InventoryItem>();

        public void Add(ItemId id, int count) {
            if (count <= 0 || !IsValidId(id)) {
                return;
            }

            var item = GetOrCreateItem(id);
            item.count += count;
        }
        
        public void Set(ItemId id, int count) {
            if (count <= 0) {
                RemoveAll(id);
                return;
            }
            
            var item = GetOrCreateItem(id);
            item.count = count;
        }

        public void Remove(ItemId id, int count) {
            if (count <= 0) {
                return;
            }
            
            var item = GetItem(id);

            if (item == null) {
                return;
            }

            item.count -= count;

            if (item.count <= 0) {
                items.Remove(item);
            }
        }

        public int GetCount(ItemId id) {
            var item = GetItem(id);
            return item == null ? 0 : item.count;
        }
        
        private bool IsValidId(ItemId id) {
            return !DefsFacade.I.Items.Get(id).IsEmpty;
        }
        
        private void RemoveAll(ItemId id) {
            var item = GetItem(id);
            if (item != null) {
                items.Remove(item);
            }
        }

        // TODO: [BG] Optimize. Probably we should use dictionary
        private InventoryItem GetItem(ItemId id) {
            foreach (var item in items) {
                if (item.id == id) {
                    return item;
                }
            }

            return null;
        }
        
        private InventoryItem GetOrCreateItem(ItemId id) {
            var item = GetItem(id);

            if (item == null) {
                item = new InventoryItem(id, 0);
                items.Add(item);
            }

            return item;
        }
    }

    [Serializable]
    public class InventoryItem {
        public ItemId id;
        public int count;

        public InventoryItem(ItemId id, int count = 0) {
            this.id = id;
            this.count = count;
        }
    }
}
