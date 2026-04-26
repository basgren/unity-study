using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Features.Bosses.VengefulSpirit.SpectralSwords {
    /// <summary>
    /// Abstract authoring contract for a spectral-sword wave. Concrete subclasses supply
    /// the <see cref="Entry"/> list (either hand-authored or procedurally generated) and
    /// per-pattern defaults (speed, max travel, lifetime). The caster treats both kinds
    /// the same, so subclasses are interchangeable in the inspector.
    /// </summary>
    public abstract class SpectralSwordPattern : ScriptableObject {
        [Serializable]
        public struct Entry {
            [Tooltip("Delay BEFORE this sword spawns, measured from the previous spawn (or cast start for the first).")]
            public float spawnDelay;

            [Tooltip("Horizontal offset from the spawn anchor, in world units.")]
            public float lateralOffset;

            [Tooltip("Vertical offset from the spawn anchor, in world units.")]
            public float verticalOffset;

            [Tooltip("Telegraph hover duration before the sword starts flying.")]
            public float telegraphTime;

            [Tooltip("Fly direction. Will be normalised at runtime. Typical: (1, -1) or (-1, -1).")]
            public Vector2 flightDirection;

            [Tooltip("Per-entry speed override. <= 0 falls back to the pattern's default speed.")]
            public float flightSpeedOverride;
        }

        public abstract IReadOnlyList<Entry> Entries { get; }
        public abstract float DefaultFlightSpeed { get; }
        public abstract float DefaultMaxTravelDistance { get; }
        public abstract float DefaultLifetime { get; }
    }
}
