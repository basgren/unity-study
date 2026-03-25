using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Models.Inventory {
    [CreateAssetMenu(menuName = "Defs/InventoryItemsDef", fileName = "InventoryItemsDef")]
    public sealed class InventoryItemsDef : ScriptableObject {
        [SerializeField]
        private List<ItemDef> items = new List<ItemDef>();

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

    [Serializable]
    public sealed class ItemDef {
        [SerializeField]
        private string id;

        [SerializeField, HideInInspector]
        private string uid;

        public string Id => id;
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
