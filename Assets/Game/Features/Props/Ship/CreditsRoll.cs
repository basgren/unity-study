using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;

namespace Game.Features.Props.Ship {
    /// <summary>
    /// Simple end-credits sequence: shows a series of text pages one at a time, each fading in,
    /// holding, then fading out. Drives a single <see cref="CanvasGroup"/> + text label.
    /// Call <see cref="Play"/> and yield on it from a cutscene coroutine; it completes after the
    /// last page has faded out (the caller owns whatever happens next, e.g. the final screen fade).
    /// </summary>
    public class CreditsRoll : MonoBehaviour {
        [SerializeField]
        private CanvasGroup canvasGroup;

        [SerializeField]
        private TextMeshProUGUI text;

        [SerializeField,
         Tooltip("Each entry is one on-screen page, referenced as a localization string. Use line " +
                 "breaks within an entry's translation for multiple lines on the same page. Pages " +
                 "are shown in order.")]
        private LocalizedString[] pages;

        [Header("Timing (seconds)")]
        [SerializeField]
        private float fadeInDuration = 0.5f;

        [Tooltip("How long each page stays fully visible before it fades out.")]
        [SerializeField]
        private float holdDuration = 3f;

        [SerializeField]
        private float fadeOutDuration = 0.5f;

        private void Awake() {
            if (canvasGroup != null) {
                canvasGroup.alpha = 0f;
            }
        }

        /// <summary>
        /// Plays every page in sequence (fade in → hold → fade out). Yields until the last page
        /// has finished. No-op if the references or page list are not set.
        /// </summary>
        public IEnumerator Play() {
            if (canvasGroup == null || text == null || pages == null) {
                yield break;
            }

            for (int i = 0; i < pages.Length; i++) {
                text.text = pages[i].GetLocalizedString();
                yield return FadeCanvas(0f, 1f, fadeInDuration);
                yield return new WaitForSeconds(holdDuration);
                yield return FadeCanvas(1f, 0f, fadeOutDuration);
            }
        }

        private IEnumerator FadeCanvas(float from, float to, float duration) {
            if (duration <= 0f) {
                canvasGroup.alpha = to;
                yield break;
            }

            float elapsed = 0f;
            canvasGroup.alpha = from;

            while (elapsed < duration) {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            canvasGroup.alpha = to;
        }
    }
}
