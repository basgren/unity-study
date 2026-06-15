using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.DebugTools {
    /// <summary>
    /// Debug-only data asset that raises player-state flags when the game starts — handy for jumping
    /// straight into a state gated behind an event flag (e.g. <c>VengefulSpiritDefeated</c>). Loaded
    /// from Resources only when enabled via <c>MainConfig.DebugSystems.EnableDebugFlags</c>;
    /// otherwise ignored entirely.
    /// </summary>
    [CreateAssetMenu(menuName = "Debug/Debug Flags", fileName = "DebugFlagConfig")]
    public sealed class DebugFlagConfig : ScriptableObject {
        /// <summary>Resources-relative path used by the loader (asset must live under Resources/Debug).</summary>
        public const string ResourcePath = "Debug/DebugFlagConfig";

        [SerializeField]
        [Tooltip("Flag names raised on the player state at startup, e.g. VengefulSpiritDefeated.")]
        private List<string> initialFlags = new();

        public IReadOnlyList<string> InitialFlags => initialFlags;
    }
}
