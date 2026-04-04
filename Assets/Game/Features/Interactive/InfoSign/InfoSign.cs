using Game.Core.Bootstrap;
using Game.Core.Components.Interaction;
using Game.UI.WoodenDialog;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace Game.Features.Interactive.InfoSign {
    public class InfoSign : InteractableBase {
        private const string DefaultTableCollectionName = "UI";

        [SerializeField]
        private WoodenBoardBig infoSignPrefab;

        [SerializeField]
        private string textKey = "world.sign.info";

        [SerializeField]
        private string tableCollectionName = DefaultTableCollectionName;
        
        public void ShowInfo() {
            var menu = G.Menu.OpenMenu(infoSignPrefab);
            menu.SetText(GetLocalizedMessage());
        }

        private string GetLocalizedMessage() {
            if (string.IsNullOrEmpty(textKey)) {
                return "";
            }

            var localized = LocalizationSettings.StringDatabase.GetLocalizedString(tableCollectionName, textKey);
            return string.IsNullOrEmpty(localized) ? textKey : localized;
        }
    }
}
