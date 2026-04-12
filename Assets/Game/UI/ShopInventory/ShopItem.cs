using Core.Models;
using Game.Core.Models.Shop;
using TMPro;
using UnityEngine;

namespace Game.UI.ShopInventory {
    /// <summary>
    /// Visual representation of a single item in the shop list.
    /// Inherits the icon/price/affordability visuals from <see cref="ShopItemView"/>.
    /// </summary>
    public class ShopItem : ShopItemView {
        [SerializeField]
        private TextMeshProUGUI itemCount;

        private ShopItemEntry entry;

        public ShopItemEntry Entry => entry;

        /// <summary>
        /// Initializes the shop item display from a shop entry definition.
        /// </summary>
        /// <param name="shopEntry">Item definition.</param>
        /// <param name="remaining">Remaining stock. -1 means unlimited.</param>
        public void Setup(ShopItemEntry shopEntry, int remaining = -1) {
            entry = shopEntry;
            var def = DefsFacade.I.Items.Get(entry.itemId);

            if (def != null) {
                itemIcon.sprite = def.Icon;
            }

            SetPrice(entry.price);
            SetCount(remaining);
            SetAffordable(true);
        }

        public void SetCount(int remaining) {
            if (itemCount == null) {
                return;
            }

            if (remaining <= 1) {
                // Unlimited (-1) or single item — hide count
                itemCount.gameObject.SetActive(false);
            } else {
                itemCount.gameObject.SetActive(true);
                itemCount.text = $"x{remaining}";
            }
        }
    }
}
