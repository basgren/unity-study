using System;
using System.Collections.Generic;
using System.Linq;
using Core.Models;
using UnityEngine;

namespace Game.Core.Models.Inventory {
    public struct InventoryChangeEvent {
        public readonly ItemId ItemId;
        public readonly int NewCount;
        public readonly int PrevCount;

        public InventoryChangeEvent(ItemId itemId, int newCount, int prevCount) {
            NewCount = newCount;
            PrevCount = prevCount;
            ItemId = itemId;
        }
    }
    
    [Serializable]
    public class Inventory {
        [SerializeField]
        private List<InventoryItem> items = new();

        public delegate void OnInventoryChanged(InventoryChangeEvent eventInfo);
        public OnInventoryChanged OnChange;
        
        public void Add(ItemId id, int count) {
            if (count <= 0 || !IsValidId(id)) {
                return;
            }

            var item = GetOrCreateItem(id);
            
            ChangeItemCount(item, item.count + count);
        }

        public void Set(ItemId id, int count) {
            if (count <= 0) {
                RemoveAll(id);
                return;
            }
            
            var item = GetOrCreateItem(id);
            ChangeItemCount(item, count);
        }

        public void Remove(ItemId id, int count) {
            if (count <= 0) {
                return;
            }
            
            var item = GetItem(id);

            if (item == null) {
                return;
            }

            ChangeItemCount(item, item.count - count);
        }

        public int GetCount(ItemId id) {
            var item = GetItem(id);
            return item == null ? 0 : item.count;
        }
        
        private void ChangeItemCount(InventoryItem item, int count) {
            var oldCount = item.count;
            
            var newCount = Mathf.Max(0, count);
            item.count = newCount;
            
            if (item.count == 0) {
                items.Remove(item);
            }
            
            OnChange?.Invoke(new InventoryChangeEvent(item.id, newCount, oldCount));
        }
        
        private bool IsValidId(ItemId id) {
            return !DefsFacade.I.Items.Get(id).IsEmpty;
        }
        
        private void RemoveAll(ItemId id) {
            var item = GetItem(id);
            ChangeItemCount(item, 0);
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

        public List<InventoryItem> GetAll(params ItemType[] usable) {
            return items.FindAll((item) => {
                var type = DefsFacade.I.Items.Get(item.id).Type;
                return usable.Contains(type);
            });
        }
    }

    [Serializable]
    public class InventoryItem {
        private static int nextUniqueId = 0;

        public ItemId id;
        public int count;
        
        /// <summary>
        /// Unique item identifier. Used to identify items in the inventory event if several items of the
        /// same type/id are present.
        /// </summary>
        public int Uid { get; private set; }

        public InventoryItem(ItemId id, int count = 0) {
            this.id = id;
            this.count = count;
            Uid = ++nextUniqueId;
        }
    }
}
