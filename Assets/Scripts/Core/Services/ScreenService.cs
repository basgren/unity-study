using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Services {
    [DisallowMultipleComponent]
    public class ScreenService : MonoBehaviour {
        private readonly Color fadeColor = Color.black;
        private readonly int sortingOrder = 10000;

        private CanvasGroup canvasGroup;

        private void Awake() {
            EnsureCanvasAndOverlay();
        }

        private void EnsureCanvasAndOverlay() {
            if (canvasGroup != null) {
                return;
            }

            // Canvas
            GameObject canvasesContainer = SceneUtils.GetOrCreateRootObject(
                AllConst.CanvasesName,
                created => {
                    DontDestroyOnLoad(created);
                    
                    Canvas canvas = created.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = sortingOrder;
                    created.AddComponent<GraphicRaycaster>();

                    canvasGroup = created.AddComponent<CanvasGroup>();
                    canvasGroup.alpha = 0f;
                    canvasGroup.blocksRaycasts = false;
                    canvasGroup.interactable = false;
                }
            );

            // Overlay Image
            GameObject overlayGo = SceneUtils.GetOrCreateObject(
                AllConst.FadeOverlayName,
                canvasesContainer.transform,
                false,
                created => {
                    Image image = created.AddComponent<Image>();
                    image.color = fadeColor;

                    RectTransform rect = image.rectTransform;
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                }
            );
        }

        // ---------------- PUBLIC API ----------------
        public Coroutine RunWhenFadeOut(float fadeOutDuration, float fadeInDuration, Func<IEnumerator> callback) {
            return StartCoroutine(RunWhenFadeOutRoutine(fadeOutDuration, fadeInDuration, callback));
        }

        public Coroutine FadeOut(float duration) {
            return StartCoroutine(FadeRoutine(canvasGroup.alpha, 1f, duration));
        }

        public Coroutine FadeIn(float duration) {
            return StartCoroutine(FadeRoutine(canvasGroup.alpha, 0f, duration));
        }

        public IEnumerator FadeOutCoroutine(float duration) {
            yield return FadeRoutine(canvasGroup.alpha, 1f, duration);
        }

        public IEnumerator FadeInCoroutine(float duration) {
            yield return FadeRoutine(canvasGroup.alpha, 0f, duration);
        }

        // ---------------- INTERNAL ----------------

        private IEnumerator FadeRoutine(float from, float to, float duration) {
            if (canvasGroup == null) {
                EnsureCanvasAndOverlay();
            }

            if (duration <= 0f) {
                canvasGroup.alpha = to;
                canvasGroup.blocksRaycasts = to > 0.001f;
                yield break;
            }

            float t = 0f;
            canvasGroup.alpha = from;
            canvasGroup.blocksRaycasts = true;

            while (t < duration) {
                t += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(t / duration);
                canvasGroup.alpha = Mathf.Lerp(from, to, normalized);
                yield return null;
            }

            canvasGroup.alpha = to;
            canvasGroup.blocksRaycasts = to > 0.001f;
        }


        private IEnumerator RunWhenFadeOutRoutine(
            float fadeOutDuration,
            float fadeInDuration,
            Func<IEnumerator> callback
        ) {
            yield return FadeOutCoroutine(fadeOutDuration);
            yield return callback();
            yield return FadeInCoroutine(fadeInDuration);
        }
    }
}
