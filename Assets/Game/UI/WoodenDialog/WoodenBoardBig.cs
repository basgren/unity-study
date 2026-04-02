using Game.Core.Bootstrap;
using Game.Core.UI;
using TMPro;
using UnityEngine.InputSystem;

namespace Game.UI.WoodenDialog {
    public class WoodenBoardBig : TweenWindow {
        private TextMeshProUGUI[] textComponents;
        private InputAction selectAction;
        private InputAction cancelAction;
        private InputAction inventoryAction;

        protected override void Awake() {
            base.Awake();

            textComponents = GetComponentsInChildren<TextMeshProUGUI>();

            if (G.Input != null) {
                selectAction = G.Input.UI.Select;
                cancelAction = G.Input.UI.Cancel;
                inventoryAction = G.Input.UI.Inventory;
            }
        }

        private void Update() {
            if (!gameObject.activeInHierarchy) {
                return;
            }

            if (WasPressedThisFrame(selectAction) ||
                WasPressedThisFrame(cancelAction) ||
                WasPressedThisFrame(inventoryAction)) {
                G.Menu.CloseTopWindow();
            }
        }

        public void SetText(string str) {
            var parsed = str.Replace("\\n", "\n");
            
            for (int i = 0; i < textComponents.Length; i++) {
                textComponents[i].text = parsed;
            }
        }

        private bool WasPressedThisFrame(InputAction action) {
            return action != null && action.WasPressedThisFrame();
        }
    }
}
