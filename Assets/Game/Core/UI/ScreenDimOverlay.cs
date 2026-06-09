using Game.Core.Bootstrap;
using Game.Core.Services.Tween;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.UI {
    /// <summary>
    /// Full-screen semi-transparent overlay that fades in while at least one open window
    /// requests background dimming (see <see cref="MenuWindow.DimsBackground"/>) and fades
    /// out otherwise. Windows that opt out (e.g. NPC dialog) keep the world fully visible.
    ///
    /// Created as the first child of the menu canvas so it always renders behind
    /// windows on that same canvas. Subscribes to <see cref="Services.Scene.MenuManager"/>'s
    /// dim-state event so consecutive window switches never flicker the overlay.
    ///
    /// Place this component on any GameObject in the Hud scene.
    /// </summary>
    public class ScreenDimOverlay : MonoBehaviour {
        [SerializeField]
        private float targetAlpha = 0.5f;

        [SerializeField]
        private float fadeDuration = 0.25f;

        private CanvasGroup canvasGroup;
        private TweenHandle tweenHandle;
        private GameObject overlayObject;

        private void Awake() {
            BuildOverlay();
        }

        private void OnEnable() {
            if (G.Menu == null) {
                return;
            }

            G.Menu.OnDimStateChanged += HandleDimStateChanged;

            canvasGroup.alpha = G.Menu.ShouldDimBackground ? targetAlpha : 0f;
        }

        private void OnDisable() {
            if (G.Menu != null) {
                G.Menu.OnDimStateChanged -= HandleDimStateChanged;
            }

            tweenHandle?.Stop();
        }

        private void HandleDimStateChanged(bool shouldDim) {
            if (shouldDim) {
                FadeIn();
            } else {
                FadeOut();
            }
        }

        private void OnDestroy() {
            if (overlayObject != null) {
                Destroy(overlayObject);
            }
        }

        private void FadeIn() {
            overlayObject.transform.SetAsFirstSibling();
            AnimateTo(targetAlpha);
        }

        private void FadeOut() {
            AnimateTo(0f);
        }

        private void AnimateTo(float to) {
            tweenHandle?.Stop();

            float from = canvasGroup.alpha;
            tweenHandle = G.Tween
                .New(fadeDuration, (eased, _) => { canvasGroup.alpha = Mathf.LerpUnclamped(from, to, eased); })
                .Ease(EaseType.OutCubic)
                .UnscaledTime(true)
                .Start();
        }

        private void BuildOverlay() {
            var menuCanvas = G.Menu.Canvas;

            overlayObject = new GameObject("DimOverlay");
            overlayObject.transform.SetParent(menuCanvas.transform, false);
            overlayObject.transform.SetAsFirstSibling();

            var rt = overlayObject.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            canvasGroup = overlayObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            var img = overlayObject.AddComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;
        }
    }
}
