using Game.Core.Utils;
using UnityEngine;

namespace Game.UI.Dialog {
    public class NextPageButton : MonoBehaviour {
        [SerializeField]
        private float frequency = 1;

        [SerializeField]
        private int amplitudePx = 3;

        private float phase;
        private Vector2 initialPosition;
        private RectTransform rectTransform;

        private void Awake() {
            rectTransform = GetComponent<RectTransform>();
            initialPosition = rectTransform.anchoredPosition;
        }

        void Update() {
            phase += (Time.unscaledDeltaTime * frequency) % 1;
            var value = MathFn.Periodic(phase) * amplitudePx;
            rectTransform.anchoredPosition = initialPosition + new Vector2(-value, 0);
        }
    }
}
