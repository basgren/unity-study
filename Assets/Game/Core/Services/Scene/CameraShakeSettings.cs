using System;
using UnityEngine;

namespace Game.Core.Services.Scene {
    /// <summary>
    /// Tunable camera shake preset: Cinemachine noise gains plus the shake envelope.
    /// The envelope holds full amplitude for (duration - decayDuration) seconds,
    /// then decays linearly to zero over decayDuration.
    /// </summary>
    [Serializable]
    public struct CameraShakeSettings {
        [Tooltip("Noise amplitude gain at full strength.")]
        public float amplitude;

        [Tooltip("Noise frequency gain while the shake is active.")]
        public float frequency;

        [Tooltip("Total shake time in seconds.")]
        public float duration;

        [Tooltip("Fade-out tail in seconds, clamped to total duration. Equal to duration = decay over the whole shake; 0 = hard stop.")]
        public float decayDuration;

        /// <summary>Default preset matching the original hardcoded shake feel.</summary>
        public static CameraShakeSettings Default => new CameraShakeSettings {
            amplitude = 1.5f,
            frequency = 3f,
            duration = 0.3f,
            decayDuration = 0.3f
        };
    }
}
