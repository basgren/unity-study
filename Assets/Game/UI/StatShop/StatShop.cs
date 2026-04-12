using System;
using System.Collections.Generic;
using Game.Core.Bootstrap;
using Game.Core.Models.Shop;
using Game.Core.UI;
using Game.Defs;
using UnityEngine;

namespace Game.UI.StatShop {
    /// <summary>
    /// Stat upgrade shop window opened by world interactables (e.g. BlessingTotem).
    /// Mirrors <see cref="ShopInventory.ShopInventory"/> but spends blue diamonds and
    /// upgrades hero stats stored in <see cref="Game.Features.Characters.Hero.PlayerState"/>.
    /// </summary>
    public class StatShop : TweenWindow {
        [SerializeField]
        private StatItem itemPrefab;

        [SerializeField]
        private Transform itemContainer;

        private StatShopDef shopDef;
        private readonly List<StatItem> spawnedItems = new();
        private readonly List<StatUpgradeEntry> visibleEntries = new();
        private int selectedIndex;
        private InputActions.UIActions uiActions;

        protected override void Awake() {
            base.Awake();
            uiActions = G.Input.UI;
        }

        /// <summary>
        /// Loads the given stat shop definition and populates the item list.
        /// </summary>
        public void LoadShop(StatShopDef def) {
            shopDef = def;

            if (shopDef == null) {
                Debug.LogWarning("StatShop: LoadShop called with a null StatShopDef.");
                return;
            }

            selectedIndex = 0;
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

            // All entries are kept visible (maxed entries show "MAX" instead of being removed).
            // Order matches the def to keep the shop layout stable across purchases.
            foreach (var entry in shopDef.Stats) {
                visibleEntries.Add(entry);
            }

            foreach (var entry in visibleEntries) {
                int currentLevel = G.Game.playerState.GetStatLevel(entry.id);
                var item = Instantiate(itemPrefab, itemContainer);
                item.Setup(entry, currentLevel);
                spawnedItems.Add(item);
            }

            selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, spawnedItems.Count - 1));
            UpdateSelection();
            UpdateAffordability();
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
        }

        private void UpdateAffordability() {
            var diamonds = G.Game.playerState.InventoryModel.GetCount(ItemIds.DiamondBlue);

            for (int i = 0; i < spawnedItems.Count; i++) {
                var item = spawnedItems[i];
                if (item.IsMaxed) {
                    // Maxed items already display the dimmed/MAX state from Setup.
                    continue;
                }

                int cost = item.Entry.GetCost(item.CurrentLevel);
                item.SetAffordable(diamonds >= cost);
            }
        }

        private void TryBuySelected() {
            if (selectedIndex < 0 || selectedIndex >= visibleEntries.Count) {
                return;
            }

            var item = spawnedItems[selectedIndex];
            if (item.IsMaxed) {
                return;
            }

            var entry = item.Entry;
            int currentLevel = item.CurrentLevel;
            int cost = entry.GetCost(currentLevel);

            var inventory = G.Game.playerState.InventoryModel;
            var diamonds = inventory.GetCount(ItemIds.DiamondBlue);

            if (diamonds < cost) {
                return;
            }

            inventory.Remove(ItemIds.DiamondBlue, cost);
            G.Game.playerState.SetStatLevel(entry.id, currentLevel + 1);

            // Push the new stat values to the live hero components (max health, melee damage).
            // Throw damage is read on each spawn, so it picks up the new value automatically.
            var hero = G.Hero != null ? G.Hero.Controller : null;
            if (hero != null) {
                hero.ApplyCurrentStats();

                // Buying a Health level fully heals the player as the upgrade reward.
                if (entry.id == StatId.Health) {
                    hero.HealToFull();
                }
            }

            // Refresh the list so the next-level cost / new MAX state are reflected.
            PopulateItems();
        }
    }
}
