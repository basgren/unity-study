using System;
using System.Collections.Generic;
using Game.Core.Models.Shop;
using UnityEngine;

namespace Game.Core.DebugTools {
    /// <summary>
    /// Debug-only data asset that pre-levels hero stats — the same stats bought in the Stat Shop —
    /// when the game starts. Loaded from Resources only when enabled via
    /// <c>MainConfig.DebugSystems.EnableDebugStats</c>; otherwise ignored entirely.
    /// </summary>
    [CreateAssetMenu(menuName = "Debug/Debug Stats", fileName = "DebugStatsConfig")]
    public sealed class DebugStatsConfig : ScriptableObject {
        /// <summary>Resources-relative path used by the loader (asset must live under Resources/Debug).</summary>
        public const string ResourcePath = "Debug/DebugStatsConfig";

        [Serializable]
        public struct InitialStat {
            public StatId statId;
            public int level;
        }

        [SerializeField]
        private List<InitialStat> initialStats = new();

        public IReadOnlyList<InitialStat> InitialStats => initialStats;
    }
}
