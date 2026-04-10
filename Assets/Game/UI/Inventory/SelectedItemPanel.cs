using System.Collections.Generic;
using Game.Core.Bootstrap;
using Game.Core.Models.Inventory;
using Game.Features.Characters.Hero.ItemUse;
using UnityEngine;

namespace Game.UI.Inventory {
    public enum SelectedPanelMode {
        Item,
        Perk,
    }

    public class SelectedItemPanel : MonoBehaviour {
        [SerializeField]
        private BackpackItemCtrl itemBox;

        [SerializeField]
        private SelectedPanelMode mode;

        private ISelectablePanelModel panelModel;

        private void Awake() {
            var state = G.Game.playerState;
            panelModel = mode == SelectedPanelMode.Item
                ? state.BackpackPanelModel
                : state.PerkPanelModel;

            panelModel.ItemsUpdated += OnItemsUpdated;
            panelModel.SelectionUpdated += OnSelectionUpdated;
            UpdateItem();
        }

        private void Update() {
            UpdateCooldown();
        }

        private void OnDestroy() {
            panelModel.ItemsUpdated -= OnItemsUpdated;
            panelModel.SelectionUpdated -= OnSelectionUpdated;
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

        private ItemUseService GetUseService() {
            return mode == SelectedPanelMode.Item
                ? G.Hero.ItemUseService
                : G.Hero.PerkUseService;
        }
    }
}
