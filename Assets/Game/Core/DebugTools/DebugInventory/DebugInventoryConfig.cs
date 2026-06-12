using System;
using System.Collections.Generic;
using Game.Core.Models.Inventory;
using UnityEngine;

namespace Game.Core.DebugTools {
    /// <summary>
    /// Debug-only data asset that lists the items seeded into the player inventory when the game
    /// starts. Loaded from Resources only when enabled via
    /// <c>MainConfig.DebugSystems.EnableDebugInventory</c>; otherwise ignored entirely.
    /// In play mode the custom editor shows and edits the live player inventory instead of this list.
    /// </summary>
    [CreateAssetMenu(menuName = "Debug/Debug Inventory", fileName = "DebugInventoryConfig")]
    public sealed class DebugInventoryConfig : ScriptableObject {
        /// <summary>Resources-relative path used by the loader (asset must live under Resources/Debug).</summary>
        public const string ResourcePath = "Debug/DebugInventoryConfig";

        [Serializable]
        public struct InitialItem {
            public ItemId itemId;
            public int count;
        }

        [SerializeField]
        private List<InitialItem> initialItems = new();

        public IReadOnlyList<InitialItem> InitialItems => initialItems;
    }
}
