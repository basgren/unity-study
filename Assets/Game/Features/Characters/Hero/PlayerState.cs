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

        /// <summary>
        /// Tracks how many times each item was purchased per shop.
        /// Parallel lists: keys are "shopId:itemId", values are purchase counts.
        /// </summary>
        [SerializeField]
        private List<string> shopPurchaseKeys = new();

        [SerializeField]
        private List<int> shopPurchaseValues = new();

        [SerializeField]
        private bool isArmed;

        private BackpackPanelModel backpackPanelModel;
        private PerkPanelModel perkPanelModel;
        public float baseMaxHealth;
        public float currentHealth;

        public InventoryModel InventoryModel => inventoryModel;
        public BackpackPanelModel BackpackPanelModel => backpackPanelModel;
        public PerkPanelModel PerkPanelModel => perkPanelModel;
        public bool IsArmed {
            get => isArmed;
            set => isArmed = value;
        }

        public PlayerState(PlayerConfig config) {
            baseMaxHealth = config.BaseMaxHealth;
            currentHealth = baseMaxHealth;
            backpackPanelModel = new(inventoryModel);
            perkPanelModel = new(inventoryModel);
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

        /// <summary>
        /// Returns how many times the given item was purchased in the given shop.
        /// </summary>
        public int GetShopPurchaseCount(string shopId, string itemId) {
            var key = $"{shopId}:{itemId}";
            int index = shopPurchaseKeys.IndexOf(key);
            return index >= 0 ? shopPurchaseValues[index] : 0;
        }

        /// <summary>
        /// Records an additional purchase of the given item in the given shop.
        /// </summary>
        public void AddShopPurchase(string shopId, string itemId) {
            var key = $"{shopId}:{itemId}";
            int index = shopPurchaseKeys.IndexOf(key);
            if (index >= 0) {
                shopPurchaseValues[index]++;
            } else {
                shopPurchaseKeys.Add(key);
                shopPurchaseValues.Add(1);
            }
        }
    }
}
