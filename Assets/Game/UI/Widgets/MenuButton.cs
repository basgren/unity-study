using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Widgets {
    public enum MenuButtonStyle {
        Normal,
        Accent,
        Danger
    }

    public class MenuButton : Button {
        [SerializeField]
        private MenuButtonStyle style = MenuButtonStyle.Normal;

        [SerializeField]
        private float pressedTextOffset = -2f;

        public MenuButtonStyle Style {
            get => style;
            set {
                if (style == value) {
                    return;
                }

                style = value;
                ApplyStyle();
            }
        }

        private float originalTextOffset;
        private RectTransform textRect;
        private Image buttonImage;
        private TextMeshProUGUI buttonText;

        protected override void Awake() {
            base.Awake();
            CacheReferences();
            ApplyStyle();
        }

        protected override void OnEnable() {
            base.OnEnable();
            ApplyStyle();
        }

#if UNITY_EDITOR
        protected override void OnValidate() {
            base.OnValidate();

            CacheReferences();
            ApplyStyle();
        }
#endif

        protected override void DoStateTransition(SelectionState state, bool instant) {
            base.DoStateTransition(state, instant);

            if (!TryCacheTextRect()) {
                return;
            }

            var offset = state == SelectionState.Pressed
                ? originalTextOffset + pressedTextOffset
                : originalTextOffset;

            textRect.anchoredPosition = new Vector2(textRect.anchoredPosition.x, offset);
            ApplyStyle();
        }

        private void ApplyStyle() {
            CacheReferences();

            if (buttonImage != null) {
                buttonImage.color = GetStyleColor(style);
            }
        }

        private static Color32 GetStyleColor(MenuButtonStyle buttonStyle) {
            switch (buttonStyle) {
                case MenuButtonStyle.Accent:
                    return new Color32(0x74, 0xB7, 0x7F, 0xFF);
                case MenuButtonStyle.Danger:
                    return new Color32(0xFF, 0x83, 0x6F, 0xFF);
                default:
                    return new Color32(0xED, 0xB5, 0x66, 0xFF);
            }
        }

        private static MenuButtonStyle ResolveStyleFromColor(Color color) {
            var color32 = (Color32)color;
            if (color32.Equals(GetStyleColor(MenuButtonStyle.Accent))) {
                return MenuButtonStyle.Accent;
            }

            if (color32.Equals(GetStyleColor(MenuButtonStyle.Danger))) {
                return MenuButtonStyle.Danger;
            }

            return MenuButtonStyle.Normal;
        }

        private bool TryCacheTextRect() {
            if (textRect != null) {
                return true;
            }

            CacheReferences();
            if (buttonText == null) {
                return false;
            }

            textRect = buttonText.rectTransform;
            originalTextOffset = textRect.anchoredPosition.y;
            return true;
        }

        private void CacheReferences() {
            if (buttonImage == null) {
                buttonImage = GetComponent<Image>();
            }

            if (buttonText == null) {
                buttonText = FindButtonText();
            }
        }

        private TextMeshProUGUI FindButtonText() {
            var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (var i = 0; i < texts.Length; i++) {
                if (texts[i].name == "ButtonText") {
                    return texts[i];
                }
            }

            return texts.Length > 0 ? texts[0] : null;
        }
    }
}
