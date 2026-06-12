using Game.Core.Bootstrap;
using Game.Core.Models.Inventory;
using Game.Defs;
using TMPro;
using UnityEngine;

namespace Game.UI.Widgets.CoinsCounter {
    public class CoinsCounter : MonoBehaviour {
        [SerializeField]
        private ItemId itemIdToCount = ItemIds.Coin;
        
        private TextMeshProUGUI[] valueTextComps;
        private InventoryModel inventory;

        private void Start() {
            valueTextComps = GetComponentsInChildren<TextMeshProUGUI>();
            inventory = G.Game.playerState.InventoryModel;
            inventory.OnChange += OnChange;

            // The HUD scene loads additively after the inventory is already
            // populated, so seed the display with the current count instead of
            // waiting for the next change event.
            SetDisplayedCount(inventory.GetCount(itemIdToCount));
        }

        private void OnDestroy() {
            inventory.OnChange -= OnChange;
        }

        private void OnChange(InventoryChangeEvent eventInfo) {
            if (eventInfo.ItemId == itemIdToCount) {
                SetDisplayedCount(eventInfo.NewCount);
            }
        }

        private void SetDisplayedCount(int count) {
            foreach (var value in valueTextComps) {
                value.text = count.ToString();
            }
        }
    }
}
