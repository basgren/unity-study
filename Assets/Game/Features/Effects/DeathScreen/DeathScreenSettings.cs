using System;
using UnityEngine;

namespace Game.Features.Effects.DeathScreen {
    /// <summary>
    /// Tuning values for the hero death sequence. Lives on <c>MainConfig</c> so it can be
    /// edited in the Inspector without owning a separate asset. <see cref="DeathScreenEffect"/>
    /// is created dynamically at runtime and therefore cannot use <c>[SerializeField]</c>
    /// directly — see AGENTS.md "Service Configuration Rules".
    /// </summary>
    [Serializable]
    public class DeathScreenSettings {
        [Header("Gameplay")]
        [Tooltip("Time.timeScale applied for the duration of the death sequence.")]
        public float SlowTimeScale = 0.7f;

        [Header("Desaturation (requires post-processing on the main camera)")]
        public float DesaturateDuration = 0.4f;

        [Header("Iris")]
        [Tooltip("Iris radius in aspect-corrected viewport units at the start of the sequence.")]
        public float InitialRadius = 0.55f;

        [Tooltip("Iris radius at the end of the shrink phase (small opening around the hero).")]
        public float FinalRadius = 0.06f;

        [Tooltip("Seconds (unscaled) to wait before the iris starts shrinking.")]
        public float IrisStartDelay = 0.1f;

        public float IrisShrinkDuration = 2.0f;

        [Range(0, 1)]
        public float VignetteDarkness = 1.0f;

        [Tooltip("Width of the dithered edge band in viewport units. Larger values make the ordered-dither pattern more visible.")]
        [Range(0, 0.2f)]
        public float EdgeWidth = 0.05f;

        [Tooltip("When enabled, the iris edge uses a Bayer 4x4 ordered dither for a stippled pixel-art look. When off, a soft smoothstep is used instead.")]
        public bool UseDither = true;

        [Header("Fade")]
        public float FadeToBlackDuration = 0.4f;

        [Tooltip("Total sequence length on unscaled time. The completion callback fires at this point.")]
        public float TotalRespawnDelay = 2.5f;
    }
}
