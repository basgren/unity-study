using Game.Core.Bootstrap;
using Game.Core.Components.Interaction;
using Game.UI.WoodenDialog;
using UnityEngine;

namespace Game.Features.Interactive.InfoSign {
    public class InfoSign : InteractableBase {
        [SerializeField]
        private WoodenBoardBig infoSignPrefab;

        [SerializeField]
        private string message = "Info";
        
        public void ShowInfo() {
            var menu = G.Menu.OpenMenu(infoSignPrefab);
            menu.SetText(message);
        }
    }
}
