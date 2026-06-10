using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.Models.Inventory {
    [CreateAssetMenu(menuName = "Defs/InventoryItemsDef", fileName = "InventoryItemsDef")]
    public sealed class InventoryItemsDef : ScriptableObject {
        [SerializeField]
        private List<ItemDef> items = new();

        [SerializeField]
        private bool generateCSharpClass;

        // Project-relative path, e.g. "Assets/Game/Defs/Inventory/Generated/ItemIds.cs"
        [SerializeField]
        private string csharpClassFile;

        [SerializeField]
        private string csharpClassName = "ItemIds";

        [SerializeField]
        private string csharpClassNamespace = "Game.Generated";

        public bool GenerateCSharpClass => generateCSharpClass;
        public string CSharpClassFile => csharpClassFile;
        public string CSharpClassName => csharpClassName;
        public string CSharpClassNamespace => csharpClassNamespace;
        public IReadOnlyList<ItemDef> Items => items;

        public ItemDef Get(string id) {
            if (string.IsNullOrEmpty(id)) {
                return null;
            }

            foreach (var itemDef in items) {
                if (itemDef != null && itemDef.Id == id) {
                    return itemDef;
                }
            }

            return null;
        }

        public string[] GetAllIds() {
            var result = new List<string>(items.Count);
            foreach (var itemDef in items) {
                if (itemDef != null && !string.IsNullOrEmpty(itemDef.Id)) {
                    result.Add(itemDef.Id);
                }
            }

            return result.ToArray();
        }
    }

    public enum ItemType {
        // When an item is collected, it's not added to the backpack (coins, keys)
        Resource,
        
        // When item is collected, it's added to backpack and can be used multiple times
        Usable,
        
        // When an item is collected, it's added to the backpack and can be used only once
        Consumable,
        
        // Used instantly on pick up (like fruits which restore health on pickup), not added to the backpack.
        Instant,

        // Activatable perk (mask, parrot, etc.). Shown in a separate perk panel, not the backpack.
        Perk,

        // Quest/key item (rusty key, etc.). Stored and shown in the backpack for reference, but cannot be
        // selected or used by the player. Removed from the inventory by the system that consumes it
        // (e.g. when its matching door is opened).
        Key,
    }

    [Serializable]
    public sealed class ItemDef {
        [SerializeField]
        private string id;

        [SerializeField]
        private Sprite icon;

        [SerializeField]
        private ItemType type = ItemType.Resource;

        [SerializeField]
        private float cooldown;

        [SerializeField, HideInInspector]
        private string uid;

        public string Id => id;
        public Sprite Icon => icon;
        public ItemType Type => type;
        public float Cooldown => cooldown;
        public string Uid => uid;
        public bool IsEmpty => string.IsNullOrEmpty(id);

#if UNITY_EDITOR
        public void EnsureUid() {
            if (string.IsNullOrEmpty(uid)) {
                uid = Guid.NewGuid().ToString("N");
            }
        }
#endif
    }
}
