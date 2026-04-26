using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Features.Bosses.VengefulSpirit.SpectralSwords {
    /// <summary>
    /// Spectral-sword wave laid out along a horizontal line above the camera. Spawns
    /// <c>swordCount</c> swords with a fixed delay and lateral offset between them. All
    /// swords share the same telegraph time, flight direction, speed, travel distance,
    /// and lifetime. Use when the wave is uniform — for varied per-sword timing or
    /// direction, use <see cref="SpectralSwordExplicitPattern"/> instead.
    /// </summary>
    [CreateAssetMenu(menuName = "Vengeful Spirit/Sword Pattern (Line)")]
    public class SpectralSwordLinePattern : SpectralSwordPattern {
        [Tooltip("Number of swords in the wave.")]
        [SerializeField, Min(0)]
        private int swordCount = 4;

        [Tooltip("Delay between consecutive sword spawns. The first sword spawns immediately at cast effect.")]
        [SerializeField, Min(0f)]
        private float spawnDelay = 0.15f;

        [Tooltip("Lateral spacing between adjacent swords, in world units. Positive value places swords left-to-right.")]
        [SerializeField]
        private float lateralOffset = 1.5f;

        [Tooltip("Lateral offset of the FIRST sword from the spawn anchor, in world units. Subsequent swords are placed at lateralStart + i * lateralOffset.")]
        [SerializeField]
        private float lateralStart = -2.25f;

        [Tooltip("Vertical offset from the spawn anchor, in world units.")]
        [SerializeField]
        private float verticalOffset;

        [Tooltip("Telegraph hover duration applied to every sword before it starts flying.")]
        [SerializeField, Min(0f)]
        private float telegraphTime = 0.4f;

        [Tooltip("Fly direction shared by every sword. Will be normalised at runtime.")]
        [SerializeField]
        private Vector2 flightDirection = new Vector2(0f, -1f);

        [SerializeField, Min(0f)]
        private float flightSpeed = 12f;

        [SerializeField, Min(0f)]
        private float maxTravelDistance = 24f;

        [Tooltip("Hard lifetime cap measured from the end of telegraph. Despawns swords that somehow can't accumulate distance.")]
        [SerializeField, Min(0f)]
        private float lifetime = 4f;

        private Entry[] cachedEntries;

        public override IReadOnlyList<Entry> Entries {
            get {
                if (cachedEntries == null || cachedEntries.Length != swordCount) {
                    Rebuild();
                }
                return cachedEntries;
            }
        }

        public override float DefaultFlightSpeed => flightSpeed;
        public override float DefaultMaxTravelDistance => maxTravelDistance;
        public override float DefaultLifetime => lifetime;

        private void OnEnable() {
            Rebuild();
        }

        private void OnValidate() {
            Rebuild();
        }

        private void Rebuild() {
            int count = Mathf.Max(0, swordCount);
            cachedEntries = new Entry[count];
            for (int i = 0; i < count; i++) {
                cachedEntries[i] = new Entry {
                    spawnDelay = i == 0 ? 0f : spawnDelay,
                    lateralOffset = lateralStart + i * lateralOffset,
                    verticalOffset = verticalOffset,
                    telegraphTime = telegraphTime,
                    flightDirection = flightDirection,
                    flightSpeedOverride = 0f
                };
            }
        }
    }
}
