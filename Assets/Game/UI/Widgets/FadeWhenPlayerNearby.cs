using Game.Core.Bootstrap;
using Game.Features.Characters.Hero;
using Game.Features.Hero;
using UnityEngine;

namespace Game.UI.Widgets {
    /// <summary>
    /// Fades a HUD element down when the player's collider overlaps the element's screen rect,
    /// so the player isn't hidden behind it, and restores opacity when the player moves out.
    /// Designed for ScreenSpaceOverlay or ScreenSpaceCamera HUDs.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(RectTransform))]
    public class FadeWhenPlayerNearby : MonoBehaviour {
        [SerializeField, Range(0f, 1f), Tooltip("Alpha used when the player overlaps the rect.")]
        private float nearAlpha = 0.1f;

        [SerializeField, Range(0f, 1f), Tooltip("Alpha used when the player is outside the rect.")]
        private float farAlpha = 1f;

        [SerializeField, Tooltip("Higher values fade faster. Per-second exponential rate.")]
        private float fadeSpeed = 8f;

        [SerializeField, Tooltip("Extra screen-space pixels added around the rect when testing overlap.")]
        private float screenPadding = 0f;

        private CanvasGroup canvasGroup;
        private RectTransform rectTransform;
        private Canvas canvas;
        private Collider2D playerCollider;
        private readonly Vector3[] worldCornersBuffer = new Vector3[4];

        private void Awake() {
            canvasGroup = GetComponent<CanvasGroup>();
            rectTransform = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>();
            canvasGroup.alpha = farAlpha;
        }

        private void OnEnable() {
            if (canvasGroup != null) {
                canvasGroup.alpha = farAlpha;
            }

            if (G.Hero != null) {
                G.Hero.OnHeroRegistered += OnHeroRegistered;
                G.Hero.OnHeroUnregistered += OnHeroUnregistered;

                if (G.Hero.Controller != null) {
                    BindHero(G.Hero.Controller);
                }
            }
        }

        private void OnDisable() {
            if (G.Hero != null) {
                G.Hero.OnHeroRegistered -= OnHeroRegistered;
                G.Hero.OnHeroUnregistered -= OnHeroUnregistered;
            }
            playerCollider = null;
        }

        private void LateUpdate() {
            float target = ResolveTargetAlpha();
            // Frame-rate independent exponential lerp toward the target.
            float t = 1f - Mathf.Exp(-fadeSpeed * Time.unscaledDeltaTime);
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, target, t);
        }

        private void OnHeroRegistered(PlayerController controller) {
            BindHero(controller);
        }

        private void OnHeroUnregistered() {
            playerCollider = null;
        }

        private void BindHero(PlayerController controller) {
            playerCollider = controller != null ? controller.GetComponent<Collider2D>() : null;
        }

        private float ResolveTargetAlpha() {
            var cam = Camera.main;
            if (playerCollider == null || cam == null || canvas == null) {
                return farAlpha;
            }

            if (!TryGetPlayerScreenRect(cam, out var playerMin, out var playerMax)) {
                return farAlpha;
            }

            GetPanelScreenRect(out var panelMin, out var panelMax);
            return RectsOverlap(playerMin, playerMax, panelMin, panelMax) ? nearAlpha : farAlpha;
        }

        private bool TryGetPlayerScreenRect(Camera cam, out Vector2 min, out Vector2 max) {
            Bounds b = playerCollider.bounds;
            // Project all four 2D corners; the camera may not be perfectly axis-aligned.
            Vector3 c0 = cam.WorldToScreenPoint(new Vector3(b.min.x, b.min.y, b.center.z));
            Vector3 c1 = cam.WorldToScreenPoint(new Vector3(b.max.x, b.min.y, b.center.z));
            Vector3 c2 = cam.WorldToScreenPoint(new Vector3(b.min.x, b.max.y, b.center.z));
            Vector3 c3 = cam.WorldToScreenPoint(new Vector3(b.max.x, b.max.y, b.center.z));

            // Behind the camera — treat as outside.
            if (c0.z < 0f && c1.z < 0f && c2.z < 0f && c3.z < 0f) {
                min = max = Vector2.zero;
                return false;
            }

            min = new Vector2(
                Mathf.Min(Mathf.Min(c0.x, c1.x), Mathf.Min(c2.x, c3.x)),
                Mathf.Min(Mathf.Min(c0.y, c1.y), Mathf.Min(c2.y, c3.y))
            );
            max = new Vector2(
                Mathf.Max(Mathf.Max(c0.x, c1.x), Mathf.Max(c2.x, c3.x)),
                Mathf.Max(Mathf.Max(c0.y, c1.y), Mathf.Max(c2.y, c3.y))
            );
            return true;
        }

        private void GetPanelScreenRect(out Vector2 min, out Vector2 max) {
            rectTransform.GetWorldCorners(worldCornersBuffer);

            // For ScreenSpaceOverlay the UI camera must be null when converting to screen space.
            Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;

            min = RectTransformUtility.WorldToScreenPoint(uiCam, worldCornersBuffer[0]);
            max = RectTransformUtility.WorldToScreenPoint(uiCam, worldCornersBuffer[2]);

            min.x -= screenPadding;
            min.y -= screenPadding;
            max.x += screenPadding;
            max.y += screenPadding;
        }

        private static bool RectsOverlap(Vector2 aMin, Vector2 aMax, Vector2 bMin, Vector2 bMax) {
            return aMax.x >= bMin.x && aMin.x <= bMax.x
                && aMax.y >= bMin.y && aMin.y <= bMax.y;
        }
    }
}
