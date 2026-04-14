using System.Collections.Generic;
using Core.Models;
using Game.Core.Models.Inventory;
using Game.Core.Utils;
using UnityEngine;

namespace Game.Features.Characters.Hero.ItemUse {
    public class ItemUseService {
        private readonly ISelectablePanelModel panelModel;
        private readonly Dictionary<ItemId, IItemUseStrategy> strategies = new();
        private readonly Dictionary<ItemId, TinyTimer> cooldowns = new();

        public ItemUseService(ISelectablePanelModel panelModel) {
            this.panelModel = panelModel;
        }

        public void Register(IItemUseStrategy strategy) {
            strategies[strategy.ItemId] = strategy;

            var itemDef = DefsFacade.I.Items.Get(strategy.ItemId);
            if (itemDef is { Cooldown: > 0 }) {
                cooldowns[strategy.ItemId] = new TinyTimer(itemDef.Cooldown);
            }
        }

        public void Update(float deltaTime) {
            foreach (var cd in cooldowns.Values) {
                cd.Update(deltaTime);
            }

            foreach (var strategy in strategies.Values) {
                strategy.Update(deltaTime);
            }
        }

        public void TryUseSelectedItem() {
            var selected = panelModel.SelectedItem;
            if (selected == null) {
                Debug.Log("No item selected.");
                return;
            }

            if (IsOnCooldown(selected.id)) {
                Debug.Log("Cooldown in progress.");
                return;
            }

            if (strategies.TryGetValue(selected.id, out var strategy) && strategy.CanUse()) {
                Debug.Log($"Using {selected.id}.");
                strategy.Use();
                StartCooldown(selected.id);
            }
        }

        public bool IsOnCooldown(ItemId itemId) {
            return cooldowns.TryGetValue(itemId, out var cd) && !cd.IsTimedOut;
        }

        /// <summary>
        /// Returns whether the registered strategy currently reports it can be used.
        /// Does not account for cooldown — that is handled separately by the UI.
        /// Returns true for unknown items so they don't appear disabled by default.
        /// </summary>
        public bool CanUse(ItemId itemId) {
            return !strategies.TryGetValue(itemId, out var strategy) || strategy.CanUse();
        }

        /// <summary>
        /// Returns cooldown progress from 0 (just started) to 1 (complete/no cooldown).
        /// </summary>
        public float GetCooldownProgress(ItemId itemId) {
            return cooldowns.TryGetValue(itemId, out var cd) ? cd.Progress : 1f;
        }

        public float GetCooldownRemaining(ItemId itemId) {
            return cooldowns.TryGetValue(itemId, out var cd) ? cd.Remaining : 0f;
        }

        private void StartCooldown(ItemId itemId) {
            if (cooldowns.TryGetValue(itemId, out var cd)) {
                cd.Start();
            }
        }
    }
}
