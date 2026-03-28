using System;
using Game.Configs;
using Game.Core.Models.Inventory;
using UnityEngine;

namespace Game.Features.Characters.Hero {
    [Serializable]
    public class PlayerState {
        [SerializeField]
        private Inventory inventory = new();
        private BackpackPanelModel backpackPanelModel; 
        public float baseMaxHealth;
        public float currentHealth;
        
        public Inventory Inventory => inventory;
        public BackpackPanelModel BackpackPanelModel => backpackPanelModel;

        public PlayerState(PlayerConfig config) {
            baseMaxHealth = config.BaseMaxHealth;
            currentHealth = baseMaxHealth;
            backpackPanelModel = new(inventory);
        }

        /// <summary>
        /// Current max health taking into accounts all buffs and level-ups.
        /// </summary>
        /// <returns></returns>
        public float GetMaxHealth() {
            return baseMaxHealth;
        }
    }
}
