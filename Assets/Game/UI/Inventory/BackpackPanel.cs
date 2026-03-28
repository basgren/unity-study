using System;
using System.Collections.Generic;
using Game.Core.Bootstrap;
using Game.Core.Models.Inventory;
using Game.Core.UI;
using UnityEngine;

namespace Game.UI.Inventory {
    public class BackpackPanel : AnimatedWindow {
        [SerializeField]
        private GameObject containerArea;
        
        [SerializeField]
        private BackpackItemCtrl itemCtrlPrefab;
        
        private BackpackPanelModel backpack;
        private readonly List<BackpackItemCtrl> items = new();
        private InputActions.UIActions uiActions; 
        
        private void Start() {
            uiActions = G.Input.UI;
            backpack = G.Game.playerState.BackpackPanelModel;
            backpack.ItemsUpdated += OnItemsUpdated;
            backpack.SelectionUpdated += OnSelectionUpdated;
            UpdateView();
        }

        private void Update() {
            if (uiActions.Inventory.WasPressedThisFrame()) {
                G.Menu.CloseTopWindow(); // Don't use Close() method, as it should be managed by MenuManager
            }

            if (uiActions.Left.WasPressedThisFrame()) {
                Debug.Log(">>>> pref");
                backpack.PrevItem();
            }
            
            if (uiActions.Right.WasPressedThisFrame()) {
                Debug.Log(">>>> next");
                backpack.NextItem();
            }
        }

        private void OnDestroy() {
            backpack.ItemsUpdated -= OnItemsUpdated;
            backpack.SelectionUpdated -= OnSelectionUpdated;
        }

        private void OnItemsUpdated(IReadOnlyList<InventoryItem> _) {
            UpdateView();
        }
        
        private void OnSelectionUpdated(InventoryItem currentItem, InventoryItem prevItem) {
            UpdateSelection();
        }

        private void UpdateView() {
            var backpackItems = backpack.Items;
            int needed = backpackItems.Count;

            // Reuse existing items, deactivate extras
            for (int i = 0; i < items.Count; i++) {
                items[i].gameObject.SetActive(i < needed);
            }

            // Create new items only if we need more
            for (int i = items.Count; i < needed; i++) {
                items.Add(Instantiate(itemCtrlPrefab, containerArea.transform));
            }

            // Update data
            for (int i = 0; i < needed; i++) {
                items[i].SetItem(backpackItems[i]);
            }

            UpdateSelection();
        }

        private void UpdateSelection() {
            Debug.Log("Updating selection:");
            foreach (var itemCtrl in items) {
                itemCtrl.SetSelected(backpack.SelectedItemUid == itemCtrl.Item.Uid);
            }
        }
    }
}
