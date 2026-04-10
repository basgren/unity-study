using System.Collections.Generic;
using Game.Core.Bootstrap;
using Game.Core.Models.Inventory;
using Game.Features.Characters.Hero.ItemUse;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI.Inventory {
    public enum SelectedPanelMode {
        Item,
        Perk,
    }

    public class SelectedItemPanel : MonoBehaviour {
        [SerializeField]
        private BackpackItemCtrl itemBox;

        [SerializeField]
        private KeyHint keyHint;

        [SerializeField]
        private SelectedPanelMode mode;

        private ISelectablePanelModel panelModel;
        private InputAction action;

        private void Awake() {
            var state = G.Game.playerState;
            panelModel = mode == SelectedPanelMode.Item
                ? state.BackpackPanelModel
                : state.PerkPanelModel;

            panelModel.ItemsUpdated += OnItemsUpdated;
            panelModel.SelectionUpdated += OnSelectionUpdated;
            UpdateItem();

            action = mode == SelectedPanelMode.Item
                ? G.Input.Player.UseItem
                : G.Input.Player.UsePerk;

            G.Input.OnSchemeChanged += OnSchemeChanged;
            RefreshKeyHint();
        }

        private void Update() {
            UpdateCooldown();
        }

        private void OnDestroy() {
            panelModel.ItemsUpdated -= OnItemsUpdated;
            panelModel.SelectionUpdated -= OnSelectionUpdated;

            if (G.Input != null) {
                G.Input.OnSchemeChanged -= OnSchemeChanged;
            }
        }

        private void OnSelectionUpdated(InventoryItem currentItem, InventoryItem prevItem) {
            UpdateItem();
        }

        private void OnItemsUpdated(IReadOnlyList<InventoryItem> obj) {
            UpdateItem();
        }

        private void UpdateItem() {
            var currentItem = panelModel.SelectedItem;
            itemBox.SetItem(currentItem);
        }

        private void UpdateCooldown() {
            var selected = panelModel.SelectedItem;
            var useService = GetUseService();

            if (selected == null || useService == null) {
                itemBox.SetCooldownProgress(1f);
                return;
            }

            itemBox.SetCooldownProgress(useService.GetCooldownProgress(selected.id));
        }

        private void OnSchemeChanged() {
            RefreshKeyHint();
        }

        private void RefreshKeyHint() {
            if (keyHint == null || G.Input == null) {
                return;
            }

            keyHint.SetFromAction(action, G.Input.CurrentSchemeBindingGroup);
        }

        private ItemUseService GetUseService() {
            return mode == SelectedPanelMode.Item
                ? G.Hero.ItemUseService
                : G.Hero.PerkUseService;
        }
    }
}
