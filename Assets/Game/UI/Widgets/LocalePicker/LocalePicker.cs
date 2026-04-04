using System;
using Game.Core.Bootstrap;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.Widgets.LocalePicker {
    /// <summary>
    /// Single-selectable cycle picker for locale selection.
    /// When focused, left/right input cycles through available locales.
    /// Arrow labels ("<" / ">") are tinted to indicate focus state.
    /// </summary>
    public class LocalePicker : Selectable, IPointerClickHandler {
        [SerializeField]
        private TMP_Text label;

        [SerializeField]
        private TMP_Text leftArrow;

        [SerializeField]
        private TMP_Text rightArrow;

        [SerializeField]
        private Color normalArrowColor = new Color(1f, 1f, 1f, 0.3f);

        [SerializeField]
        private Color highlightedArrowColor = Color.white;

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

        public void OnLanguageChanged(string value) {
            G.Locale.SetLocale(value);
        }

        public void OnPointerClick(PointerEventData eventData) {
            CycleLocale(1);
        }

        public override void OnMove(AxisEventData eventData) {
            switch (eventData.moveDir) {
                case MoveDirection.Left:
                    CycleLocale(-1);
                    break;
                case MoveDirection.Right:
                    CycleLocale(1);
                    break;
                default:
                    base.OnMove(eventData);
                    break;
            }
        }

        protected override void DoStateTransition(SelectionState state, bool instant) {
            base.DoStateTransition(state, instant);

            var color = state == SelectionState.Highlighted
                     || state == SelectionState.Selected
                     || state == SelectionState.Pressed
                ? highlightedArrowColor
                : normalArrowColor;

            SetArrowColor(leftArrow, color);
            SetArrowColor(rightArrow, color);
            SetArrowColor(label, color);
        }

        private void CycleLocale(int delta) {
            if (locales == null || locales.Length == 0) {
                return;
            }

            currentIndex = (currentIndex + delta + locales.Length) % locales.Length;
            UpdateLabel();
            onLocaleChanged?.Invoke(locales[currentIndex].code);
        }

        private void UpdateLabel() {
            if (label != null && currentIndex >= 0 && currentIndex < locales.Length) {
                label.text = locales[currentIndex].displayName;
            }
        }

        private static void SetArrowColor(TMP_Text arrow, Color color) {
            if (arrow != null) {
                arrow.color = color;
            }
        }

        [Serializable]
        public struct LocaleEntry {
            public string code;
            public string displayName;
        }
    }
}
