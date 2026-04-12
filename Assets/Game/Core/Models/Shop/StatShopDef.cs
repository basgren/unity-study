using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace Game.Core.Models.Shop {
    /// <summary>
    /// Identifies a stat that can be upgraded at a stat shop. Used as the persistent
    /// key in <see cref="Game.Features.Characters.Hero.PlayerState"/>.
    /// </summary>
    public enum StatId {
        Health,
        MeleeDamage,
        ThrowDamage,
    }

    /// <summary>
    /// Defines the stats available for purchase at a stat shop (e.g. BlessingTotem).
    /// Loaded as a direct asset reference from the world interactable.
    /// </summary>
    [CreateAssetMenu(menuName = "Defs/StatShopDef", fileName = "StatShopDef")]
    public class StatShopDef : ScriptableObject {
        [SerializeField]
        private List<StatUpgradeEntry> stats = new();

        public IReadOnlyList<StatUpgradeEntry> Stats => stats;
    }

    [Serializable]
    public class StatUpgradeEntry {
        public StatId id;
        public LocalizedString displayName;
        public Sprite icon;
        public int maxLevel = 1;

        /// <summary>
        /// Cost in blue diamonds to reach the next level. Index 0 is the cost to go from
        /// level 0 to level 1, index 1 is the cost to go from 1 to 2, etc. If the array is
        /// empty or shorter than the current level, the last entry (or 1 if empty) is used.
        /// </summary>
        public int[] costPerLevel;

        /// <summary>
        /// Returns the cost in blue diamonds to upgrade from <paramref name="currentLevel"/>
        /// to the next level.
        /// </summary>
        public int GetCost(int currentLevel) {
            if (costPerLevel == null || costPerLevel.Length == 0) {
                return 1;
            }

            int idx = Mathf.Min(currentLevel, costPerLevel.Length - 1);
            return costPerLevel[idx];
        }
    }
}
