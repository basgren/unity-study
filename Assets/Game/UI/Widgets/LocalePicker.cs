using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.UI.Widgets {
    /// <summary>
    /// A left/right cycle picker for selecting a locale.
    /// Configure available locales in the Inspector.
    /// </summary>
    public class LocalePicker : MonoBehaviour {
        [SerializeField]
        private TMP_Text label;

        [SerializeField]
        private Button leftButton;

        [SerializeField]
        private Button rightButton;

        [SerializeField]
        private LocaleEntry[] locales;

        [SerializeField]
        private UnityEvent<string> onLocaleChanged;

        private int currentIndex;

        public UnityEvent<string> OnLocaleChanged => onLocaleChanged;

        /// <summary>
        /// Gets or sets the current locale code. Setting it updates the display.
        /// </summary>
        public string Value {
            get => currentIndex >= 0 && currentIndex < locales.Length
                ? locales[currentIndex].code
                : "";
            set {
                for (int i = 0; i < locales.Length; i++) {
                    if (locales[i].code == value) {
                        currentIndex = i;
                        UpdateLabel();
                        return;
                    }
                }
            }
        }

        private void OnEnable() {
            if (leftButton != null) {
                leftButton.onClick.AddListener(OnLeftClick);
            }

            if (rightButton != null) {
                rightButton.onClick.AddListener(OnRightClick);
            }
        }

        private void OnDisable() {
            if (leftButton != null) {
                leftButton.onClick.RemoveListener(OnLeftClick);
            }

            if (rightButton != null) {
                rightButton.onClick.RemoveListener(OnRightClick);
            }
        }

        private void OnLeftClick() {
            if (locales.Length == 0) {
                return;
            }

            currentIndex = (currentIndex - 1 + locales.Length) % locales.Length;
            UpdateLabel();
            onLocaleChanged?.Invoke(locales[currentIndex].code);
        }

        private void OnRightClick() {
            if (locales.Length == 0) {
                return;
            }

            currentIndex = (currentIndex + 1) % locales.Length;
            UpdateLabel();
            onLocaleChanged?.Invoke(locales[currentIndex].code);
        }

        private void UpdateLabel() {
            if (label != null && currentIndex >= 0 && currentIndex < locales.Length) {
                label.text = locales[currentIndex].displayName;
            }
        }

        [Serializable]
        public struct LocaleEntry {
            public string code;
            public string displayName;
        }
    }
}
