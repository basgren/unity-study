using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.ShopInventory {
    /// <summary>
    /// Shared visuals for a single row in any shop-style window: icon, price text,
    /// selection highlight and an "unaffordable" dim/red state. Concrete subclasses
    /// (e.g. <see cref="ShopItem"/>, <see cref="Game.UI.StatShop.StatItem"/>) add
    /// their own data binding via a Setup method.
    /// </summary>
    public abstract class ShopItemView : MonoBehaviour {
        [SerializeField]
        protected Image itemIcon;

        [SerializeField]
        protected TextMeshProUGUI itemCost;

        [SerializeField]
        protected GameObject selectionObject;

        [SerializeField]
        protected Color normalPriceColor = Color.white;

        [SerializeField]
        protected Color unaffordablePriceColor = Color.red;

        public void SetPrice(int price) {
            itemCost.text = price.ToString();
        }

        public void SetSelected(bool isSelected) {
            selectionObject.SetActive(isSelected);
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
