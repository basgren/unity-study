using System.Collections.Generic;
using UnityEngine;

namespace Game.Features.Bosses.VengefulSpirit.SpectralSwords {
    /// <summary>
    /// Spectral-sword wave authored as an explicit list of entries — full per-sword control.
    /// Use when each sword needs distinct timing, position, or direction.
    /// </summary>
    [CreateAssetMenu(menuName = "Vengeful Spirit/Sword Pattern (Explicit)")]
    public class SpectralSwordExplicitPattern : SpectralSwordPattern {
        [SerializeField]
        private Entry[] entries;

        [SerializeField]
        private float defaultFlightSpeed = 12f;

        [SerializeField]
        private float defaultMaxTravelDistance = 24f;

        [Tooltip("Hard lifetime cap measured from the end of telegraph. Despawns swords that somehow can't accumulate distance.")]
        [SerializeField]
        private float defaultLifetime = 4f;

        public override IReadOnlyList<Entry> Entries => entries;
        public override float DefaultFlightSpeed => defaultFlightSpeed;
        public override float DefaultMaxTravelDistance => defaultMaxTravelDistance;
        public override float DefaultLifetime => defaultLifetime;
    }
}
