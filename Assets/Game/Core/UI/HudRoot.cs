using Game.Core.Bootstrap;
using Game.Core.Services.Tween;
using UnityEngine;

namespace Game.Core.UI {
    /// <summary>
    /// Marks the root of the in-game HUD and lets cutscenes fade the whole HUD (health bar,
    /// coin/diamond counts, selected item and perk panels — every child of this object) out and
    /// back in via <see cref="Services.Scene.HudService"/>.
    ///
    /// Place this component on the HUDCanvas GameObject in the Hud scene. It self-registers with
    /// <see cref="Bootstrap.G.Hud"/> while enabled, so the service forwards Show/Hide here without
    /// needing a serialized reference. Fades a <see cref="CanvasGroup"/> (added at runtime if the
    /// object does not already have one) using the same tween pattern as <see cref="ScreenDimOverlay"/>.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class HudRoot : MonoBehaviour {
        [SerializeField]
        [Tooltip("Seconds for the HUD to fade out or in when a cutscene hides/shows it.")]
        private float fadeDuration = 0.3f;

        private CanvasGroup canvasGroup;
        private TweenHandle tweenHandle;

        private void Awake() {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void OnEnable() {
            if (G.Hud != null) {
                G.Hud.RegisterRoot(this);
            }
        }

        private void OnDisable() {
            if (G.Hud != null) {
                G.Hud.UnregisterRoot(this);
            }

            tweenHandle?.Stop();
        }

        /// <summary>
        /// Fades the HUD to fully visible or fully hidden. Pass <paramref name="instant"/> to snap
        /// without a tween (used when applying a state requested before this root existed).
        /// </summary>
        public void SetVisible(bool visible, bool instant) {
            float target = visible ? 1f : 0f;

            if (instant) {
                tweenHandle?.Stop();
                canvasGroup.alpha = target;
                return;
            }

            AnimateTo(target);
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
    }
}
