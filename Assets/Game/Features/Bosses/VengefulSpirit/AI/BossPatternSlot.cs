using System;
using Game.Features.Bosses.VengefulSpirit.AI.Patterns;
using UnityEngine;

namespace Game.Features.Bosses.VengefulSpirit.AI {
    /// <summary>
    /// Strategic-layer scheduling entry. Pairs a pattern component (the "what") with the
    /// rules that govern how often it can run inside a phase pool (the "when"). Designers
    /// build phase pools as arrays of these slots on <see cref="VengefulSpiritAI"/>.
    /// </summary>
    [Serializable]
    public struct BossPatternSlot {
        [Tooltip("Pattern component to invoke for this slot. Drag in a sibling component or " +
                 "any GameObject hosting a VengefulSpiritPattern subclass.")]
        public VengefulSpiritPattern pattern;

        [Tooltip("Max uses per cycle. -1 = no cap. Use 0 to disable a slot temporarily " +
                 "without removing it from the array.")]
        public int maxPerCycle;

        [Tooltip("Max consecutive uses (back-to-back without an intervening different pattern). " +
                 "-1 = no limit. Use 1 to forbid repeats; 2 for the spec's 'max 2 common attacks " +
                 "in a row' rule; etc.")]
        public int maxInARow;

        [Tooltip("Relative weight when multiple slots are eligible. 0 or negative is treated as 1.")]
        public float weight;
    }
}
