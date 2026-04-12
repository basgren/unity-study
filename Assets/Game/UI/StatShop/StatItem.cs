using Game.Core.Models.Shop;
using Game.UI.ShopInventory;
using Game.UI.Widgets.ProgressBar;
using TMPro;
using UnityEngine;

namespace Game.UI.StatShop {
    /// <summary>
    /// Visual representation of a single upgradeable stat in the stat shop list.
    /// Reuses <see cref="ShopItemView"/> for icon/price/selection visuals and adds
    /// a stat name, current/max level text, and a "MAX" indicator that replaces
    /// the price when the stat has reached its cap.
    /// </summary>
    public class StatItem : ShopItemView {
        [SerializeField]
        private TextMeshProUGUI statName;

        [SerializeField]
        private TextMeshProUGUI levelText;

        [SerializeField, Tooltip("Shown in place of the price when the stat is at max level.")]
        private GameObject maxLabel;

        [SerializeField, Tooltip("Bar reflecting current/max level.")]
        private ProgressBar progressBar;

        public StatUpgradeEntry Entry { get; private set; }
        public int CurrentLevel { get; private set; }
        public bool IsMaxed => Entry != null && CurrentLevel >= Entry.maxLevel;

        public void Setup(StatUpgradeEntry entry, int currentLevel) {
            Entry = entry;
            CurrentLevel = currentLevel;

            itemIcon.sprite = entry.icon;

            if (statName != null && entry.displayName != null && !entry.displayName.IsEmpty) {
                statName.text = entry.displayName.GetLocalizedString();
            } else if (statName != null) {
                statName.text = entry.id.ToString();
            }

            if (levelText != null) {
                levelText.text = $"{currentLevel}/{entry.maxLevel}";
            }

            if (progressBar != null) {
                float progress = entry.maxLevel > 0 ? (float)currentLevel / entry.maxLevel : 0f;
                progressBar.SetProgress(progress);
            }

            if (IsMaxed) {
                if (maxLabel != null) {
                    maxLabel.SetActive(true);
                }
                itemCost.gameObject.SetActive(false);
                // Dim the icon to mirror the regular "unaffordable" look.
                SetAffordable(false);
            } else {
                if (maxLabel != null) {
                    maxLabel.SetActive(false);
                }
                itemCost.gameObject.SetActive(true);
                SetPrice(entry.GetCost(currentLevel));
                SetAffordable(true);
            }
        }
    }
}
