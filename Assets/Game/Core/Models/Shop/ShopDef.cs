using System;
using System.Collections.Generic;
using Game.Core.Models.Inventory;
using UnityEngine;
using UnityEngine.Localization;

namespace Game.Core.Models.Shop {
    /// <summary>
    /// Defines items available for purchase at a shop.
    /// Loaded from Resources via <c>Resources.Load&lt;ShopDef&gt;($"Shops/{shopId}")</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Defs/ShopDef", fileName = "ShopDef")]
    public class ShopDef : ScriptableObject {
        [SerializeField]
        private List<ShopItemEntry> items = new();

        public IReadOnlyList<ShopItemEntry> Items => items;
    }

    [Serializable]
    public class ShopItemEntry {
        public ItemId itemId;
        public int price;

        /// <summary>
        /// Maximum number of this item available for purchase. 0 means unlimited.
        /// </summary>
        public int maxCount = 1;

        /// <summary>
        /// Optional gate: when set, this entry only appears in the shop once the player
        /// has the matching flag raised (see <c>PlayerState.HasFlag</c>). Empty means the
        /// entry is always available.
        /// </summary>
        public string requiredFlag;

        public LocalizedString description;
    }
}
