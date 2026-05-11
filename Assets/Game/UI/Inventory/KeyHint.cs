using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI.Inventory {
    [Serializable]
    public struct ControllerSpriteEntry {
        public string controlPath;
        public Sprite sprite;
    }

    public class KeyHint : MonoBehaviour {
        private const string GamepadGroup = "Gamepad";
        private const float DisabledAlpha = 0.5f;

        [SerializeField]
        private Image keyImage;

        [SerializeField]
        private TextMeshProUGUI keyText;

        [SerializeField]
        private ControllerSpriteEntry[] controllerSprites;

        private Sprite keyboardSprite;
        private RectTransform rectTransform;
        private bool initialized;

        private void Awake() {
            EnsureInitialized();
        }

        // Awake order between sibling GameObjects is not guaranteed; in builds the order can
        // differ from the Editor. SelectedItemPanel.Awake calls into KeyHint synchronously
        // (RefreshKeyHint → SetFromAction → ShowKeyText/ShowControllerSprite), which can
        // arrive here before our own Awake has run. Lazy-init guards against that.
        private void EnsureInitialized() {
            if (initialized) {
                return;
            }
            keyboardSprite = keyImage.sprite;
            rectTransform = GetComponent<RectTransform>();
            initialized = true;
        }

        public void SetFromAction(InputAction action, string bindingGroup) {
            EnsureInitialized();
            if (bindingGroup == GamepadGroup) {
                string path = FindBindingPath(action, GamepadGroup);
                Sprite sprite = FindControllerSprite(path);
                if (sprite != null) {
                    ShowControllerSprite(sprite);
                    return;
                }
            }

            string display = action.GetBindingDisplayString(
                InputBinding.DisplayStringOptions.DontUseShortDisplayNames,
                bindingGroup
            );
            ShowKeyText(display);
        }

        /// <summary>
        /// Dims the key icon and label to indicate the bound action cannot be used.
        /// </summary>
        public void SetEnabled(bool enabled) {
            float a = enabled ? 1f : DisabledAlpha;

            var imgColor = keyImage.color;
            imgColor.a = a;
            keyImage.color = imgColor;

            var txtColor = keyText.color;
            txtColor.a = a;
            keyText.color = txtColor;
        }

        private void ShowControllerSprite(Sprite sprite) {
            keyImage.sprite = sprite;
            rectTransform.sizeDelta = sprite.rect.size;
            keyText.gameObject.SetActive(false);
        }

        private void ShowKeyText(string text) {
            keyImage.sprite = keyboardSprite;
            rectTransform.sizeDelta = keyboardSprite.rect.size;
            keyText.gameObject.SetActive(true);

            if (text.Length > 0) {
                keyText.text = text[0].ToString();
            }
        }

        private static string FindBindingPath(InputAction action, string group) {
            foreach (var binding in action.bindings) {
                if (!binding.isComposite && binding.groups.Contains(group)) {
                    return binding.effectivePath;
                }
            }
            return null;
        }

        private Sprite FindControllerSprite(string path) {
            if (path == null) {
                return null;
            }
            foreach (var entry in controllerSprites) {
                if (entry.controlPath == path) {
                    return entry.sprite;
                }
            }
            return null;
        }
    }
}
