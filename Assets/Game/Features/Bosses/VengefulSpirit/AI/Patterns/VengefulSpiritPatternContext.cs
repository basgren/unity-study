using System.Collections;
using UnityEngine;

namespace Game.Features.Bosses.VengefulSpirit.AI.Patterns {
    /// <summary>
    /// Mutable carrier passed to every pattern. Holds:
    /// - the action-layer boss reference (<see cref="Boss"/>) for Request* calls;
    /// - the strategic-layer reference (<see cref="AI"/>) for movement overrides and
    ///   anchor pick helpers — patterns avoid duplicating those;
    /// - current <see cref="Phase"/> (read-only from the pattern's perspective);
    /// - per-cycle scratch state (<see cref="CycleState"/>).
    ///
    /// Built once on the AI side at <c>Awake</c> / <c>OnEnable</c> and reused for every
    /// pattern run — the strategic layer mutates <see cref="Phase"/> when entering phase 2
    /// and bumps <see cref="CycleState"/> when patterns complete.
    /// </summary>
    public class VengefulSpiritPatternContext {
        public VengefulSpirit Boss;
        public VengefulSpiritAI AI;
        public VengefulSpiritPhase Phase;
        public VengefulSpiritCycleState CycleState;

        public Transform Player => AI != null ? AI.GetPlayer() : null;
        public bool BossDead => Boss == null || Boss.IsDead;

        /// <summary>
        /// Two-phase wait that bridges the AI's command-pulse / Request* cycle:
        /// 1. Wait for the boss to BECOME busy (bounded by a 0.5s deadline so a swallowed
        ///    request — e.g. RequestSwordCast with a missing anchor — never soft-locks).
        /// 2. Wait for the boss to FINISH being busy.
        /// Lifted verbatim from the original AI.WaitForBusyCycle.
        /// </summary>
        public IEnumerator WaitForBusyCycle() {
            float deadline = Time.time + 0.5f;
            while (Boss != null && !Boss.IsBusy && Time.time < deadline) {
                if (BossDead) {
                    yield break;
                }
                yield return null;
            }
            while (Boss != null && Boss.IsBusy) {
                if (BossDead) {
                    yield break;
                }
                yield return null;
            }
        }
    }
}
