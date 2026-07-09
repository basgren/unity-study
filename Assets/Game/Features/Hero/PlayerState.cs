using System;
using System.Collections.Generic;
using Game.Configs;
using Game.Core.Models.Inventory;
using Game.Core.Models.Shop;
using UnityEngine;

namespace Game.Features.Characters.Hero {
    [Serializable]
    public class PlayerState {
        [SerializeField]
        private InventoryModel inventoryModel = new();

        [SerializeField]
        private List<string> flags = new();

        [SerializeField]
        private List<string> seenDialogNodes = new();

        /// <summary>
        /// Tracks how many times each item was purchased per shop.
        /// Parallel lists: keys are "shopId:itemId", values are purchase counts.
        /// </summary>
        [SerializeField]
        private List<string> shopPurchaseKeys = new();

        [SerializeField]
        private List<int> shopPurchaseValues = new();

        /// <summary>
        /// Levels of upgradeable hero stats. Parallel lists: keys are <see cref="StatId"/>
        /// names, values are the current level (default 0). Same shape as
        /// <see cref="shopPurchaseKeys"/> so it survives Unity serialization.
        /// </summary>
        [SerializeField]
        private List<string> statLevelKeys = new();

        [SerializeField]
        private List<int> statLevelValues = new();

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
            RebuildTransient();
        }

        /// <summary>
        /// Recreates the non-serialized panel models from the current inventory. Must be
        /// called after this state is produced by deserialization (e.g. JsonUtility.FromJson),
        /// which bypasses the constructor and leaves these models null.
        /// </summary>
        public void RebuildTransient() {
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

        /// <summary>All raised flags. Exposed for persistence.</summary>
        public IReadOnlyList<string> Flags => flags;

        /// <summary>Global keys ("dialogId.nodeId") of one-shot dialog nodes already shown.</summary>
        public IReadOnlyList<string> SeenDialogNodes => seenDialogNodes;

        public bool HasSeenNode(string key) {
            return seenDialogNodes.Contains(key);
        }

        public void MarkNodeSeen(string key) {
            if (!seenDialogNodes.Contains(key)) {
                seenDialogNodes.Add(key);
            }
        }

        /// <summary>Replaces all flags with the given set (used by save/restore).</summary>
        public void RestoreFlags(IEnumerable<string> values) {
            flags.Clear();
            flags.AddRange(values);
        }

        /// <summary>Replaces all seen-node keys with the given set (used by save/restore).</summary>
        public void RestoreSeenDialogNodes(IEnumerable<string> values) {
            seenDialogNodes.Clear();
            seenDialogNodes.AddRange(values);
        }

        /// <summary>
        /// Current max health taking into accounts all buffs and level-ups.
        /// </summary>
        public float GetMaxHealth() {
            return baseMaxHealth + GetStatLevel(StatId.Health);
        }

        /// <summary>
        /// Bonus melee damage from stat upgrades. Added on top of the weapon's base damage.
        /// </summary>
        public int GetMeleeDamageBonus() {
            return GetStatLevel(StatId.MeleeDamage);
        }

        /// <summary>
        /// Bonus throw damage from stat upgrades. Added on top of the projectile's base damage.
        /// </summary>
        public int GetThrowDamageBonus() {
            return GetStatLevel(StatId.ThrowDamage);
        }

        /// <summary>
        /// Returns the current level of the given stat. 0 if it has never been upgraded.
        /// </summary>
        public int GetStatLevel(StatId stat) {
            var key = stat.ToString();
            int index = statLevelKeys.IndexOf(key);
            return index >= 0 ? statLevelValues[index] : 0;
        }

        /// <summary>
        /// Sets the current level of the given stat (used by saving/restoring and the stat shop).
        /// </summary>
        public void SetStatLevel(StatId stat, int level) {
            var key = stat.ToString();
            int index = statLevelKeys.IndexOf(key);
            if (index >= 0) {
                statLevelValues[index] = level;
            } else {
                statLevelKeys.Add(key);
                statLevelValues.Add(level);
            }
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
        /// Total number of purchases made in the given shop, across all of its items.
        /// </summary>
        public int GetTotalShopPurchases(string shopId) {
            var prefix = shopId + ":";
            int total = 0;
            for (int i = 0; i < shopPurchaseKeys.Count; i++) {
                if (shopPurchaseKeys[i].StartsWith(prefix)) {
                    total += shopPurchaseValues[i];
                }
            }

            return total;
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
