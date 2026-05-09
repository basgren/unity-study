using System.Collections;
using Game.Features.Bosses.VengefulSpirit.Teleport;
using UnityEngine;

namespace Game.Features.Bosses.VengefulSpirit.AI.Patterns {
    /// <summary>
    /// "Boss stays in the same place. Starts casting magic. Spawns spectral shield."
    /// Used as the forced phase-2 opener. The strategic layer invokes this pattern's
    /// <see cref="Run"/> directly when the HP threshold is crossed; the cycle picker
    /// excludes it via cap = 0 in both phases by default so it never spawns mid-cycle.
    ///
    /// Owns its own <see cref="centerAnchor"/> reference — the pattern teleports the boss
    /// to the central position before casting. If left null, the boss casts in place.
    /// </summary>
    public class SpawnShieldPattern : VengefulSpiritPattern {
        [Header("Spawn Shield")]
        [Tooltip("Anchor the boss is teleported to before spawning the shield. Typically a " +
                 "central position out of the player's reach. If null, the boss casts in place.")]
        [SerializeField]
        private TeleportAnchor centerAnchor;

        public override bool CanRun(VengefulSpiritPatternContext ctx) {
            return !ctx.Boss.HasActiveShield;
        }

        public override IEnumerator Run(VengefulSpiritPatternContext ctx) {
            if (centerAnchor != null) {
                ctx.Boss.RequestTeleport(centerAnchor);
                yield return ctx.WaitForBusyCycle();
                if (ctx.BossDead) {
                    yield break;
                }
            }

            ctx.Boss.RequestSpawnShield();
            yield return ctx.WaitForBusyCycle();
        }
    }
}
