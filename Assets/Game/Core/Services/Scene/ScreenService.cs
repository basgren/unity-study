using System;
using System.Collections;
using Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.Services.Scene {
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
                CoreConst.CanvasesName,
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
                CoreConst.FadeOverlayName,
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
        /// <summary>
        /// Fades the screen out, runs the callback while fully faded, then fades back in.
        /// Optionally invokes a final action after the fade-in completes.
        /// </summary>
        /// <param name="fadeOutDuration">Fade-out duration in seconds.</param>
        /// <param name="fadeInDuration">Fade-in duration in seconds.</param>
        /// <param name="callback">Work to perform while the screen is fully faded out.</param>
        /// <param name="afterFadeIn">Optional action invoked after the fade-in is complete.</param>
        public Coroutine RunWhenFadeOut(
            float fadeOutDuration,
            float fadeInDuration,
            Func<IEnumerator> callback,
            Action afterFadeIn = null
        ) {
            return StartCoroutine(RunWhenFadeOutRoutine(fadeOutDuration, fadeInDuration, callback, afterFadeIn));
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

            float startTime = Time.realtimeSinceStartup;
            canvasGroup.alpha = from;
            canvasGroup.blocksRaycasts = true;

            while (true) {
                // Use realtimeSinceStartup so a blocking scene load does not collapse the first fade frame.
                float elapsed = Time.realtimeSinceStartup - startTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                canvasGroup.alpha = Mathf.Lerp(from, to, normalized);

                if (normalized >= 1f) {
                    break;
                }

                yield return null;
            }

            canvasGroup.alpha = to;
            canvasGroup.blocksRaycasts = to > 0.001f;
        }

        private IEnumerator RunWhenFadeOutRoutine(
            float fadeOutDuration,
            float fadeInDuration,
            Func<IEnumerator> callback,
            Action afterFadeIn
        ) {
            yield return FadeOutCoroutine(fadeOutDuration);
            yield return callback();
            yield return FadeInCoroutine(fadeInDuration);
            afterFadeIn?.Invoke();
        }
    }
}
