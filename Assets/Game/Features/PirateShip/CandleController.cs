using Core.Components.Animation;
using UnityEngine;

namespace Game.Features.PirateShip {
    /// <summary>
    /// Switches a candle prefab between lit and unlit visual states.
    /// Lit: the body sprite-frame animation runs and the flame light is visible.
    /// Unlit: the animator is paused on a static unlit sprite and the flame is hidden.
    ///
    /// Used by scripted scenes (e.g. the boss intro cutscene) that need to flip
    /// candles on or off in response to gameplay events.
    /// </summary>
    public class CandleController : MonoBehaviour {
        [SerializeField]
        private SpriteRenderer bodyRenderer;

        [Tooltip("Sprite shown on the candle body when unlit. The lit body sprite is driven " +
                 "by the body animator at runtime, so it does not need a separate field.")]
        [SerializeField]
        private Sprite unlitSprite;

        [Tooltip("Drives the body's lit-frame animation. Disabled while unlit so the animator " +
                 "does not overwrite the static unlit sprite on its next tick.")]
        [SerializeField]
        private SimpleSpriteAnimator bodyAnimator;

        [Tooltip("Child GameObject holding the animated flame sprite. Hidden while unlit.")]
        [SerializeField]
        private GameObject lightSprite;

        [Tooltip("Child GameObject holding the Light2D + flicker. Hidden while unlit.")]
        [SerializeField]
        private GameObject lightObject;

        [Tooltip("Initial state in the inspector; doubles as the runtime state. Toggle at runtime " +
                 "via the Lit property to switch the candle on or off.")]
        [SerializeField]
        private bool lit = true;

        /// <summary>
        /// Current lit state. Assigning applies the visual change immediately.
        /// </summary>
        public bool Lit {
            get {
                return lit;
            }
            set {
                if (lit == value) {
                    return;
                }
                lit = value;
                Apply();
            }
        }

        private void Awake() {
            Apply();
        }

        // Inspector edits write directly to the serialized field, bypassing the property
        // setter — so we re-apply here to keep the visual state in sync when designers
        // toggle Lit in the Inspector (during Play mode or while authoring the prefab).
        private void OnValidate() {
            Apply();
        }

        private void Apply() {
            if (lightSprite != null) {
                lightSprite.SetActive(lit);
            }

            if (lightObject != null) {
                lightObject.SetActive(lit);
            }

            if (bodyAnimator != null) {
                bodyAnimator.enabled = lit;
            }

            // Only force the unlit sprite while unlit — lit playback is animator-driven.
            if (!lit && bodyRenderer != null && unlitSprite != null) {
                bodyRenderer.sprite = unlitSprite;
            }
        }
    }
}
