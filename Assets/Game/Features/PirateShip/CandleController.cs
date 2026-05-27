using System.Collections;
using Game.Core.Components.Animation;
using Game.Core.Components.Effects;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Features.PirateShip {
    /// <summary>
    /// Switches a candle prefab between lit and unlit visual states.
    /// Lit: the body sprite-frame animation runs and the flame light is visible.
    /// Unlit: the animator is paused on a static unlit sprite and the flame is hidden.
    ///
    /// Runtime <see cref="Lit"/> toggles fade both the Light2D's intensity and
    /// the flame sprite alpha synchronously over <see cref="fadeDuration"/> seconds.
    /// This relies on the candle's Light2D being authored with Multiply blend +
    /// Additive overlap operation so intensity = 0 contributes nothing instead of
    /// producing a black-spot artifact.
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

        [Tooltip("SpriteRenderer of the animated flame. Its color alpha is faded in lockstep " +
                 "with the Light2D intensity so the visible flame appears/disappears together " +
                 "with the actual room illumination.")]
        [SerializeField]
        private SpriteRenderer flameRenderer;

        [Tooltip("Child GameObject holding the Light2D + flicker. Hidden while unlit.")]
        [SerializeField]
        private GameObject lightObject;

        [Tooltip("Initial state in the inspector; doubles as the runtime state. Toggle at runtime " +
                 "via the Lit property to switch the candle on or off.")]
        [SerializeField]
        private bool lit = true;

        [Tooltip("Seconds the fade takes when Lit is toggled at runtime. Both the Light2D " +
                 "intensity and the flame sprite alpha ramp over this duration. Set to 0 for " +
                 "an instant change.")]
        [Min(0f)]
        [SerializeField]
        private float fadeDuration = 0.5f;

        // Cached at Awake so the runtime fade does not have to walk children each toggle.
        // litIntensity captures the authored Inspector intensity so the fade-in target is
        // exactly what designers set on the prefab.
        private Light2D candleLight;
        private FireLightFlicker flicker;
        private float litIntensity;
        private bool cached;

        private Coroutine fadeCo;

        /// <summary>
        /// Current lit state. Assigning at runtime fades both light and flame over <c>fadeDuration</c>.
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
                ApplyAnimated();
            }
        }

        /// <summary>
        /// Sets the lit state instantly, with no fade. Use for state restore so candles appear
        /// already lit on scene entry instead of igniting.
        /// </summary>
        public void SetLitImmediate(bool value) {
            lit = value;
            CacheReferences();
            ApplyImmediate();
        }

        private void Awake() {
            CacheReferences();
            ApplyImmediate();
        }

        // Inspector edits write directly to the serialized field, bypassing the property
        // setter. Re-apply instantly so designers can preview the on/off state both at
        // edit time and during Play mode without firing a fade.
        private void OnValidate() {
            ApplyImmediate();
        }

        private void CacheReferences() {
            if (cached) {
                return;
            }
            cached = true;

            if (lightObject != null) {
                candleLight = lightObject.GetComponent<Light2D>();
                flicker = lightObject.GetComponent<FireLightFlicker>();
                if (candleLight != null) {
                    litIntensity = candleLight.intensity;
                }
            }
        }

        private void ApplyImmediate() {
            if (fadeCo != null) {
                StopCoroutine(fadeCo);
                fadeCo = null;
            }

            if (lightSprite != null) {
                lightSprite.SetActive(lit);
            }
            if (lightObject != null) {
                lightObject.SetActive(lit);
            }

            if (bodyAnimator != null) {
                bodyAnimator.enabled = lit;
            }
            // Lit body is animator-driven; only force a sprite when unlit.
            if (!lit && bodyRenderer != null && unlitSprite != null) {
                bodyRenderer.sprite = unlitSprite;
            }

            if (candleLight != null) {
                candleLight.enabled = lit;
                candleLight.intensity = lit ? litIntensity : 0f;
            }
            if (flicker != null) {
                flicker.enabled = lit;
            }

            SetFlameAlpha(lit ? 1f : 0f);
        }

        private void ApplyAnimated() {
            CacheReferences();

            // Coroutines need the GameObject active to run; fall back to instant if we
            // can't fade.
            if (!isActiveAndEnabled || fadeDuration <= 0f) {
                ApplyImmediate();
                return;
            }

            if (fadeCo != null) {
                StopCoroutine(fadeCo);
            }
            fadeCo = StartCoroutine(FadeFlameCo(lit));
        }

        // Synchronously fades Light2D.intensity and flame sprite alpha. Requires the
        // candle's Light2D to be configured with Additive overlap operation so the
        // intensity = 0 endpoint contributes nothing (instead of producing a dark spot
        // under AlphaBlend overlap). The flicker is held disabled across the fade so it
        // does not overwrite the lerped intensity each frame; on re-enable, its OnEnable
        // re-caches the now-correct baseline.
        private IEnumerator FadeFlameCo(bool toLit) {
            if (toLit) {
                if (bodyAnimator != null) {
                    bodyAnimator.enabled = true;
                }
                if (lightSprite != null) {
                    lightSprite.SetActive(true);
                }
                if (lightObject != null) {
                    lightObject.SetActive(true);
                }
                if (candleLight != null) {
                    candleLight.enabled = true;
                    candleLight.intensity = 0f;
                }
                SetFlameAlpha(0f);
            }
            // Flicker stays off through the fade in either direction so it cannot fight
            // our per-frame intensity writes.
            if (flicker != null) {
                flicker.enabled = false;
            }

            float startAlpha = GetFlameAlpha();
            float startIntensity = candleLight != null ? candleLight.intensity : 0f;
            float targetAlpha = toLit ? 1f : 0f;
            float targetIntensity = toLit ? litIntensity : 0f;

            float t = 0f;
            while (t < fadeDuration) {
                t += Time.deltaTime;
                float k = t / fadeDuration;
                SetFlameAlpha(Mathf.Lerp(startAlpha, targetAlpha, k));
                if (candleLight != null) {
                    candleLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, k);
                }
                yield return null;
            }
            SetFlameAlpha(targetAlpha);
            if (candleLight != null) {
                candleLight.intensity = targetIntensity;
            }

            if (toLit) {
                // Re-enabling fires FireLightFlicker.OnEnable which re-caches its baseline
                // from the current (now full litIntensity) value, so the flicker wobbles
                // around the right number going forward.
                if (flicker != null) {
                    flicker.enabled = true;
                }
            } else {
                if (candleLight != null) {
                    candleLight.enabled = false;
                }
                if (bodyAnimator != null) {
                    bodyAnimator.enabled = false;
                }
                if (bodyRenderer != null && unlitSprite != null) {
                    bodyRenderer.sprite = unlitSprite;
                }
                if (lightObject != null) {
                    lightObject.SetActive(false);
                }
                if (lightSprite != null) {
                    lightSprite.SetActive(false);
                }
            }

            fadeCo = null;
        }

        private void SetFlameAlpha(float alpha) {
            if (flameRenderer == null) {
                return;
            }
            Color c = flameRenderer.color;
            c.a = alpha;
            flameRenderer.color = c;
        }

        private float GetFlameAlpha() {
            return flameRenderer != null ? flameRenderer.color.a : 0f;
        }
    }
}
