using System.Collections;

namespace Game.Features.Bosses.VengefulSpirit.AI.Patterns {
    /// <summary>
    /// Tactical-layer pattern. Common contract for all boss-behavior patterns — the name
    /// is intentionally boss-agnostic so future bosses can reuse this interface without
    /// renaming. The concrete base class and pattern-context type are still boss-specific
    /// today (<see cref="VengefulSpiritPattern"/>, <see cref="VengefulSpiritPatternContext"/>);
    /// the interface is the seam that lets multiple bosses share the same strategic-layer
    /// machinery if and when that becomes worth doing.
    ///
    /// Patterns are stateless w.r.t. cycle state — the strategic layer owns per-cycle
    /// usage counters, consecutive-common-attack tracking, last-pattern guard, and cycle
    /// reset. A pattern's <see cref="CanRun"/> only reports its own intrinsic preconditions
    /// (phase compatibility, anchors wired, player exists, etc.).
    ///
    /// Lifecycle:
    /// - Strategic layer calls <see cref="CanRun"/> to filter the candidate list.
    /// - Strategic layer calls <see cref="Run"/> as a coroutine; the pattern uses
    ///   <see cref="VengefulSpiritPatternContext.WaitForBusyCycle"/> to block until each
    ///   driven action completes.
    /// - Patterns must yield safely if the boss dies mid-run;
    ///   <see cref="VengefulSpiritPatternContext.WaitForBusyCycle"/> handles that, but
    ///   pattern-internal waits should also poll <see cref="VengefulSpiritPatternContext.BossDead"/>.
    /// </summary>
    public interface IBossBehaviorPattern {
        /// <summary>Display name for inspector lists and debug logs. Default is the type name.</summary>
        string Id { get; }

        /// <summary>True when the pattern's intrinsic preconditions are satisfied
        /// (anchors wired, player reachable, etc.). Cycle caps, max-in-a-row rules, and
        /// phase membership live in the strategic layer (<see cref="VengefulSpiritAI"/>) —
        /// patterns answer only the question "could I physically run right now?".</summary>
        bool CanRun(VengefulSpiritPatternContext ctx);

        /// <summary>Drive the boss through the move. Should consist of action-layer calls
        /// (Request* on <see cref="VengefulSpirit"/>) and short waits, with no decision-making
        /// about whether to run again.</summary>
        IEnumerator Run(VengefulSpiritPatternContext ctx);
    }
}
