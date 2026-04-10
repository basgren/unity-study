using Core.Models;
using Game.Core.Models.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.ShopInventory {
    /// <summary>
    /// Visual representation of a single item in the shop list.
    /// </summary>
    public class ShopItem : MonoBehaviour {
        [SerializeField]
        private Image itemIcon;

        [SerializeField]
        private TextMeshProUGUI itemCost;
        
        [SerializeField]
        private TextMeshProUGUI itemCount;

        [SerializeField]
        private GameObject selectionObject;

        [SerializeField]
        private Color normalPriceColor = Color.white;

        [SerializeField]
        private Color unaffordablePriceColor = Color.red;

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

        public void SetSelected(bool isSelected) {
            selectionObject.SetActive(isSelected);
        }

        public void SetPrice(int price) {
            itemCost.text = price.ToString();
        }

        /// <summary>
        /// Updates visual state based on whether the player can afford this item.
        /// Unaffordable: icon 50% transparent, price text red.
        /// </summary>
        public void SetAffordable(bool canAfford) {
            var iconColor = itemIcon.color;
            iconColor.a = canAfford ? 1f : 0.5f;
            itemIcon.color = iconColor;

            itemCost.color = canAfford ? normalPriceColor : unaffordablePriceColor;
        }
    }
}
