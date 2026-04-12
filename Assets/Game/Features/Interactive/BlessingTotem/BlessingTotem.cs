using Game.Core.Bootstrap;
using Game.Core.Components.Interaction;
using Game.Core.Models.Shop;
using Game.UI.StatShop;
using UnityEngine;

namespace Game.Features.Interactive.BlessingTotem {
    /// <summary>
    /// World interactable that opens the stat upgrade shop. Players spend blue diamonds
    /// here to permanently improve hero stats (max health, melee damage, throw damage).
    /// </summary>
    public class BlessingTotem : InteractableBase {
        [SerializeField, Tooltip("Stat upgrades offered by this totem.")]
        private StatShopDef statShopDef;

        protected override void DoInteract() {
            if (statShopDef == null) {
                Debug.LogWarning("BlessingTotem: statShopDef is not assigned.", this);
                return;
            }

            var prefab = G.Config.StatShopPanel;
            if (prefab == null) {
                Debug.LogWarning("BlessingTotem: MainConfig.StatShopPanel is not assigned.", this);
                return;
            }

            var window = G.Menu.OpenMenu(prefab);
            if (window is StatShop statShop) {
                statShop.LoadShop(statShopDef);
            }
        }
    }
}
