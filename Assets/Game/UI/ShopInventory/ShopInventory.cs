using System;
using System.Collections.Generic;
using Core.Models;
using Game.Core.Bootstrap;
using Game.Core.Models.Inventory;
using Game.Core.Models.Shop;
using Game.Core.UI;
using Game.Defs;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;

namespace Game.UI.ShopInventory {
    /// <summary>
    /// Shop window that displays purchasable items and handles buy transactions.
    /// Opened by <see cref="Game.Core.Services.Dialog.DialogService"/> via the OpenShop dialog action.
    /// </summary>
    public class ShopInventory : TweenWindow {
        [SerializeField]
        private TextMeshProUGUI description;

        [SerializeField]
        private ShopItem itemPrefab;

        [SerializeField]
        private Transform itemContainer;

        [SerializeField]
        private LocalizedString notEnoughCoinsText;

        private ShopDef shopDef;
        private string shopId;
        private readonly List<ShopItem> spawnedItems = new();
        private readonly List<ShopItemEntry> visibleEntries = new();
        private int selectedIndex;
        private InputActions.UIActions uiActions;

        protected override void Awake() {
            base.Awake();
            uiActions = G.Input.UI;
        }

        /// <summary>
        /// Loads shop data and populates the item list.
        /// </summary>
        public void LoadShop(string id) {
            shopId = id;
            shopDef = Resources.Load<ShopDef>($"Shops/{shopId}");

            if (shopDef == null) {
                Debug.LogWarning($"ShopInventory: ShopDef not found at 'Shops/{shopId}'.");
                return;
            }

            PopulateItems();
        }

        private void Update() {
            if (uiActions.Up.WasPressedThisFrame()) {
                MoveSelection(-1);
            }

            if (uiActions.Down.WasPressedThisFrame()) {
                MoveSelection(1);
            }

            if (uiActions.Select.WasPressedThisFrame()) {
                TryBuySelected();
            }
        }

        private void PopulateItems() {
            ClearItems();

            // Collect visible entries and sort by price ascending
            foreach (var entry in shopDef.Items) {
                // Flag-gated entries stay hidden until the player has unlocked them.
                if (!string.IsNullOrEmpty(entry.requiredFlag)
                    && !G.Game.playerState.HasFlag(entry.requiredFlag)) {
                    continue;
                }

                int remaining = GetRemainingStock(entry);
                if (remaining == 0) {
                    continue;
                }

                visibleEntries.Add(entry);
            }

            visibleEntries.Sort((a, b) => a.price.CompareTo(b.price));

            foreach (var entry in visibleEntries) {
                int remaining = GetRemainingStock(entry);
                var item = Instantiate(itemPrefab, itemContainer);
                item.Setup(entry, remaining);
                spawnedItems.Add(item);
            }

            selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, spawnedItems.Count - 1));
            UpdateSelection();
            UpdateAffordability();
        }

        /// <summary>
        /// Returns remaining stock for an entry. -1 means unlimited, 0 means depleted.
        /// </summary>
        private int GetRemainingStock(ShopItemEntry entry) {
            if (entry.maxCount <= 0) {
                return -1; // unlimited
            }

            int bought = G.Game.playerState.GetShopPurchaseCount(shopId, entry.itemId);
            int remaining = entry.maxCount - bought;
            return remaining > 0 ? remaining : 0;
        }

        private void ClearItems() {
            foreach (var item in spawnedItems) {
                Destroy(item.gameObject);
            }

            spawnedItems.Clear();
            visibleEntries.Clear();
        }

        private void MoveSelection(int delta) {
            if (spawnedItems.Count == 0) {
                return;
            }

            selectedIndex = (selectedIndex + delta + spawnedItems.Count) % spawnedItems.Count;
            UpdateSelection();
        }

        private void UpdateSelection() {
            for (int i = 0; i < spawnedItems.Count; i++) {
                spawnedItems[i].SetSelected(i == selectedIndex);
            }

            UpdateDescription();
        }

        private void UpdateDescription() {
            if (selectedIndex < 0 || selectedIndex >= visibleEntries.Count) {
                description.text = "";
                return;
            }

            var entry = visibleEntries[selectedIndex];

            if (entry.description != null && !entry.description.IsEmpty) {
                description.text = entry.description.GetLocalizedString();
            } else {
                description.text = "";
            }
        }

        private void UpdateAffordability() {
            var coins = G.Game.playerState.InventoryModel.GetCount(ItemIds.Coin);

            foreach (var item in spawnedItems) {
                item.SetAffordable(coins >= item.Entry.price);
            }
        }

        private void TryBuySelected() {
            if (selectedIndex < 0 || selectedIndex >= visibleEntries.Count) {
                return;
            }

            var entry = visibleEntries[selectedIndex];
            var inventory = G.Game.playerState.InventoryModel;
            var coins = inventory.GetCount(ItemIds.Coin);

            if (coins < entry.price) {
                if (notEnoughCoinsText != null && !notEnoughCoinsText.IsEmpty) {
                    description.text = notEnoughCoinsText.GetLocalizedString();
                }

                return;
            }

            inventory.Remove(ItemIds.Coin, entry.price);
            inventory.Add(entry.itemId, 1);

            G.Game.playerState.AddShopPurchase(shopId, entry.itemId);

            // Refresh the list (depleted items will be removed)
            PopulateItems();
        }
    }
}
