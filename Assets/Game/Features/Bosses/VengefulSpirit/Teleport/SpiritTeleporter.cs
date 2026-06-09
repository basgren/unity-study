using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Features.Bosses.VengefulSpirit.Teleport {
    /// <summary>
    /// Drives the spirit's teleport sequence: fade out, reposition while
    /// invisible, hidden hold, fade in. Pure coroutine runner — the boss
    /// controller owns all action-gating and damage-immunity bookkeeping.
    ///
    /// During the fade-out, a one-shot <c>onDamageGraceElapsed</c> callback
    /// fires once the configured grace window has passed. The boss uses that
    /// to flip <c>Damageable.IgnoreDamage</c> on, so the boss can still take
    /// a final hit during the first slice of the disappear.
    /// </summary>
    public class SpiritTeleporter : MonoBehaviour {
        [SerializeField]
        private SpriteRenderer spriteRenderer;

        [Tooltip("Transform that will be repositioned to the destination at the end of the fade-out. Usually the boss root.")]
        [SerializeField]
        private Transform teleportRoot;

        [Header("Timings")]
        [Tooltip("How long the boss takes to fade to alpha 0 (the 'disappear' phase).")]
        [SerializeField]
        private float fadeOutDuration = 1.0f;

        [Tooltip("Time from the start of the fade-out at which the boss becomes damage-immune. " +
                 "Until this elapses, the boss can still take a final hit. Clamped to [0, fadeOutDuration].")]
        [SerializeField]
        private float damageGraceDuration = 0.1f;

        [Tooltip("Time spent fully hidden (alpha 0) before fading back in.")]
        [SerializeField]
        private float hiddenDuration = 2.0f;

        [Tooltip("How long the boss takes to fade from alpha 0 back to 1 (the 'reappear' phase).")]
        [SerializeField]
        private float fadeInDuration = 1.0f;

        private Coroutine activeRun;

        // Extra renderers (e.g., the spectral shield) that fade together with the boss.
        // Registered dynamically because the shield is spawned at runtime.
        private readonly List<SpriteRenderer> additionalRenderers = new List<SpriteRenderer>();

        public bool IsRunning => activeRun != null;

        /// <summary>
        /// Registers an extra <see cref="SpriteRenderer"/> whose alpha will be driven in
        /// lockstep with the main sprite during fade-in / fade-out. Used by the boss to
        /// fade the active shield together with itself when teleporting.
        /// </summary>
        public void RegisterFader(SpriteRenderer renderer) {
            if (renderer == null || additionalRenderers.Contains(renderer)) {
                return;
            }
            additionalRenderers.Add(renderer);
        }

        /// <summary>
        /// Reverses <see cref="RegisterFader"/>. Safe to call with a null or unknown
        /// renderer.
        /// </summary>
        public void UnregisterFader(SpriteRenderer renderer) {
            if (renderer == null) {
                return;
            }
            additionalRenderers.Remove(renderer);
        }

        /// <summary>
        /// Runs the full disappear/reposition/reappear sequence.
        /// <paramref name="onDamageGraceElapsed"/> fires once, partway through
        /// the fade-out, when the boss should become damage-immune.
        /// <paramref name="onAtDestination"/> fires once after the boss is fully invisible
        /// at the destination, BEFORE the hidden hold and fade-in. Use it to apply visual
        /// state that should be in place when the boss reappears (facing direction,
        /// animator parameters, etc.). May be <c>null</c>.
        /// <paramref name="onComplete"/> fires once when alpha is fully restored.
        /// <paramref name="hiddenDurationOverride"/> replaces the configured
        /// <c>hiddenDuration</c> for this run when non-negative. Pass a negative value
        /// to use the inspector default.
        /// No-op if a run is already in progress.
        /// </summary>
        public void Run(Vector3 destination, Action onDamageGraceElapsed, Action onAtDestination, Action onComplete, float hiddenDurationOverride = -1f) {
            if (activeRun != null) {
                return;
            }
            activeRun = StartCoroutine(RunSequence(destination, onDamageGraceElapsed, onAtDestination, onComplete, hiddenDurationOverride));
        }

        /// <summary>
        /// Runs only the fade-out half of the sequence — no reposition, no hidden hold,
        /// no fade-in. Leaves the sprite (and any registered fader renderers) at alpha 0.
        /// The caller is responsible for reappearing later via <see cref="Run"/>.
        /// <paramref name="onDamageGraceElapsed"/> fires the same way as in <see cref="Run"/>.
        /// </summary>
        public void FadeOutInPlace(Action onDamageGraceElapsed, Action onComplete) {
            if (activeRun != null) {
                return;
            }
            activeRun = StartCoroutine(FadeOutInPlaceSequence(onDamageGraceElapsed, onComplete));
        }

        /// <summary>
        /// Aborts any active sequence and snaps alpha back to 1 so the sprite
        /// is never left invisible. Does NOT invoke the completion callback —
        /// the caller is responsible for clearing its own state (e.g. on death).
        /// </summary>
        public void Cancel() {
            if (activeRun != null) {
                StopCoroutine(activeRun);
                activeRun = null;
            }
            SetAlpha(1f);
        }

        private IEnumerator RunSequence(Vector3 destination, Action onDamageGraceElapsed, Action onAtDestination, Action onComplete, float hiddenDurationOverride) {
            yield return FadeOutWithGrace(onDamageGraceElapsed);

            // Reposition while invisible — no visual seam.
            if (teleportRoot != null) {
                teleportRoot.position = destination;
            }

            // Apply at-destination visual state (facing, etc.) while still fully invisible
            // so the boss reappears already configured.
            onAtDestination?.Invoke();

            float effectiveHidden = hiddenDurationOverride >= 0f ? hiddenDurationOverride : hiddenDuration;
            if (effectiveHidden > 0f) {
                yield return new WaitForSeconds(effectiveHidden);
            }

            yield return Fade(0f, 1f, fadeInDuration);

            activeRun = null;
            onComplete?.Invoke();
        }

        private IEnumerator FadeOutInPlaceSequence(Action onDamageGraceElapsed, Action onComplete) {
            yield return FadeOutWithGrace(onDamageGraceElapsed);
            activeRun = null;
            onComplete?.Invoke();
        }

        // Single fade pass that also fires the grace callback at the right moment.
        // Splitting this into two Fade(...) calls would double-write alpha at the
        // boundary and risk a 1-frame visual hiccup, hence the inline loop.
        // Starts the fade from the current alpha so a follow-up teleport that begins
        // with the boss already invisible doesn't snap the sprite back to opaque.
        private IEnumerator FadeOutWithGrace(Action onDamageGraceElapsed) {
            float startAlpha = GetCurrentAlpha();
            float grace = Mathf.Clamp(damageGraceDuration, 0f, fadeOutDuration);
            bool graceFired = false;

            if (fadeOutDuration <= 0f) {
                SetAlpha(0f);
                onDamageGraceElapsed?.Invoke();
                yield break;
            }

            float t = 0f;
            while (t < fadeOutDuration) {
                t += Time.deltaTime;
                SetAlpha(Mathf.Lerp(startAlpha, 0f, t / fadeOutDuration));
                if (!graceFired && t >= grace) {
                    graceFired = true;
                    onDamageGraceElapsed?.Invoke();
                }
                yield return null;
            }
            SetAlpha(0f);
            if (!graceFired) {
                onDamageGraceElapsed?.Invoke();
            }
        }

        private float GetCurrentAlpha() {
            return spriteRenderer != null ? spriteRenderer.color.a : 1f;
        }

        private IEnumerator Fade(float from, float to, float duration) {
            if (duration <= 0f) {
                SetAlpha(to);
                yield break;
            }
            float t = 0f;
            while (t < duration) {
                t += Time.deltaTime;
                SetAlpha(Mathf.Lerp(from, to, t / duration));
                yield return null;
            }
            SetAlpha(to);
        }

        private void SetAlpha(float a) {
            ApplyAlpha(spriteRenderer, a);

            for (int i = additionalRenderers.Count - 1; i >= 0; i--) {
                SpriteRenderer r = additionalRenderers[i];
                if (r == null) {
                    // Renderer destroyed (e.g. shield despawned mid-fade) — drop it.
                    additionalRenderers.RemoveAt(i);
                    continue;
                }
                ApplyAlpha(r, a);
            }
        }

        private static void ApplyAlpha(SpriteRenderer r, float a) {
            if (r == null) {
                return;
            }
            Color c = r.color;
            c.a = a;
            r.color = c;
        }
    }
}
