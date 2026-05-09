using System.Collections;
using UnityEngine;

namespace Game.Features.Bosses.VengefulSpirit.AI.Patterns {
    /// <summary>
    /// Abstract MonoBehaviour base for tactical patterns. Each pattern is its own component
    /// dropped onto the boss GameObject; subclasses hold ONLY tactical config (anchor refs,
    /// timings, colliders relevant to this specific move). Per-phase scheduling — which
    /// patterns belong to which phase, per-cycle caps, max-in-a-row, the phase-2 opener —
    /// lives on <see cref="VengefulSpiritAI"/>, configured per slot in the inspector.
    ///
    /// No <c>[DisallowMultipleComponent]</c>: the boss may carry several different pattern
    /// subclasses (and even multiple instances of the same subclass with different tunings);
    /// the AI references each by direct ref through its slot config.
    /// </summary>
    public abstract class VengefulSpiritPattern : MonoBehaviour, IBossBehaviorPattern {
        /// <summary>Display name for inspector lists / debug logs. Subclasses may override.</summary>
        public virtual string Id => GetType().Name;

        /// <summary>
        /// Whether the AI's <c>followUpPattern</c> may fire after this pattern completes.
        /// Default true — most patterns benefit from a situational punish. Patterns that
        /// already include a strike (e.g. Blink) set this to false so the boss doesn't
        /// double-hit on the same beat.
        /// </summary>
        public virtual bool AllowFollowUp => true;

        public abstract bool CanRun(VengefulSpiritPatternContext ctx);
        public abstract IEnumerator Run(VengefulSpiritPatternContext ctx);
    }
}
