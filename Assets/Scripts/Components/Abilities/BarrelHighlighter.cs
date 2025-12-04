using System.Collections;
using UnityEngine;

namespace Components.Abilities {
    public enum BarrelHighlightMode {
        None,
        Hover,
        Interact,
        Alert
    }

    [RequireComponent(typeof(SpriteRenderer))]
    public class BarrelHighlighter : MonoBehaviour {
        [SerializeField]
        private Color normalColor = Color.white;

        [SerializeField]
        private Color highlightColor = Color.yellow;

        [SerializeField]
        private Color alertColor = Color.red;

        [SerializeField]
        private float pulseFrequency = 1f;

        private Coroutine pulseCoroutine;
        private SpriteRenderer spriteRenderer;

        void Awake() {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void SetHighlighted(BarrelHighlightMode mode) {
            switch (mode) {
                case BarrelHighlightMode.Hover:
                    StopPulse();
                    // SetColor(highlightColor);
                    StartPulse(highlightColor);
                    break;

                case BarrelHighlightMode.Interact:
                    StopPulse();
                    SetColor(Color.Lerp(highlightColor, normalColor, 0.5f));
                    break;

                case BarrelHighlightMode.Alert:
                    StartPulse(alertColor);
                    break;

                default:
                    StopPulse();
                    SetColor(normalColor);
                    break;
            }
        }

        private void StartPulse(Color targetColor) {
            if (pulseCoroutine != null) {
                StopCoroutine(pulseCoroutine);
            }

            pulseCoroutine = StartCoroutine(PulseRoutine(targetColor));
        }

        private void StopPulse() {
            if (pulseCoroutine == null) {
                return;
            }

            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }

        private IEnumerator PulseRoutine(Color targetColor) {
            float t = 0f;

            while (true) {
                t += Time.deltaTime;

                float phase = Mathf.Sin(2f * Mathf.PI * pulseFrequency * t);
                float lerp = phase  * 0.5f + 0.5f;
                // lerp = lerp * 0.5f + 0.5f;

                SetColor(Color.Lerp(normalColor, targetColor, lerp));

                yield return null;
            }
        }

        private void SetColor(Color color) {
            if (spriteRenderer != null) {
                spriteRenderer.color = color;
            }
        }
    }
}
