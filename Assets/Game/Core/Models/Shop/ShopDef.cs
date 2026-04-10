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

        public LocalizedString description;
    }
}
