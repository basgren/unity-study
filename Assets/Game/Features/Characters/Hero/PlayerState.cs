using System;
using System.Collections.Generic;
using Game.Configs;
using Game.Core.Models.Inventory;
using UnityEngine;

namespace Game.Features.Characters.Hero {
    [Serializable]
    public class PlayerState {
        [SerializeField]
        private InventoryModel inventoryModel = new();

        [SerializeField]
        private List<string> flags = new();

        private BackpackPanelModel backpackPanelModel;
        public float baseMaxHealth;
        public float currentHealth;
        
        public InventoryModel InventoryModel => inventoryModel;
        public BackpackPanelModel BackpackPanelModel => backpackPanelModel;

        public PlayerState(PlayerConfig config) {
            baseMaxHealth = config.BaseMaxHealth;
            currentHealth = baseMaxHealth;
            backpackPanelModel = new(inventoryModel);
        }

        public bool HasFlag(string flag) {
            return flags.Contains(flag);
        }

        public void SetFlag(string flag) {
            if (!flags.Contains(flag)) {
                flags.Add(flag);
            }
        }

        public void ClearFlag(string flag) {
            flags.Remove(flag);
        }

        /// <summary>
        /// Current max health taking into accounts all buffs and level-ups.
        /// </summary>
        public float GetMaxHealth() {
            return baseMaxHealth;
        }
    }
}
