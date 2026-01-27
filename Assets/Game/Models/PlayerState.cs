using System;
using Core.Models.Inventory;
using Game.Configs;
using UnityEngine;

namespace Game.Models {
    [Serializable]
    public class PlayerState {
        [SerializeField]
        private Inventory inventory = new Inventory();
        public float baseMaxHealth;
        public float currentHealth;
        
        public Inventory Inventory => inventory;

        public PlayerState(PlayerConfig config) {
            baseMaxHealth = config.baseMaxHealth;
            currentHealth = baseMaxHealth;
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
