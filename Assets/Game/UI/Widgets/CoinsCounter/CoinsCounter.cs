using Game.Core.Bootstrap;
using Game.Core.Models.Inventory;
using TMPro;
using UnityEngine;

namespace Game.UI.Widgets.CoinsCounter {
    public class CoinsCounter : MonoBehaviour {
        private TextMeshProUGUI[] valueTextComps;
        private InventoryModel inventory;

        private void Start() {
            valueTextComps = GetComponentsInChildren<TextMeshProUGUI>();
            inventory = G.Game.playerState.InventoryModel;
            inventory.OnChange += OnChange;
        }

        private void OnDestroy() {
            inventory.OnChange -= OnChange;
        }

        private void OnChange(InventoryChangeEvent eventInfo) {
            if (eventInfo.ItemId == "Coin") {
                foreach (var value in valueTextComps) {
                    value.text = eventInfo.NewCount.ToString();
                }
            }
        }
    }
}
