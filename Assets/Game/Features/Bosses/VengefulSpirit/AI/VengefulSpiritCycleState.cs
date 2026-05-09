using Game.Features.Bosses.VengefulSpirit.AI.Patterns;

namespace Game.Features.Bosses.VengefulSpirit.AI {
    /// <summary>
    /// Mutable per-cycle scratch state owned by the strategic layer. Tracks which pattern
    /// just ran and how many consecutive runs of it have happened so far — both used by
    /// the picker to enforce per-slot max-in-a-row rules.
    /// </summary>
    public class VengefulSpiritCycleState {
        /// <summary>The pattern that ran last; null at cycle start.</summary>
        public VengefulSpiritPattern LastPattern;

        /// <summary>Consecutive count for <see cref="LastPattern"/> — incremented when the
        /// same pattern runs back-to-back, reset to 1 when a different pattern runs.</summary>
        public int LastPatternConsecutive;

        public void Reset() {
            LastPattern = null;
            LastPatternConsecutive = 0;
        }
    }
}
