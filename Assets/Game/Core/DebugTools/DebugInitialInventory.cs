using System;
using System.Collections.Generic;
using Game.Core.Bootstrap;
using Game.Core.Models.Inventory;
using UnityEngine;

namespace Game.Core.DebugTools {
    /// <summary>
    /// Debug helper that seeds the player inventory with predefined items on scene start.
    /// Drop this component anywhere in the scene and configure the list in the Inspector.
    /// Remove or disable before shipping.
    /// </summary>
    public class DebugInitialInventory : MonoBehaviour {
        [Serializable]
        public struct Entry {
            public ItemId itemId;
            public int count;
        }

        [SerializeField] private List<Entry> items = new();

        private void Start() {
            var inventory = G.Game.playerState.InventoryModel;

            foreach (var entry in items) {
                if (entry.itemId.IsEmpty || entry.count <= 0) {
                    continue;
                }

                inventory.Add(entry.itemId, entry.count);
            }
        }
    }
}
