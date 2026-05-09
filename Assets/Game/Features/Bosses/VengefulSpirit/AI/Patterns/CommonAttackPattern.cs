using System.Collections;
using UnityEngine;

namespace Game.Features.Bosses.VengefulSpirit.AI.Patterns {
    /// <summary>
    /// Situational punish strike. Designed to be wired as the AI's <c>followUpPattern</c>
    /// rather than a regular cycle slot — fires immediately after a main pattern completes
    /// IF the player is currently inside <see cref="punishZone"/>. The boss is already in
    /// position from the previous pattern, so this pattern just triggers the strike; no
    /// approach drift.
    /// </summary>
    public class CommonAttackPattern : VengefulSpiritPattern {
        [Header("Common Attack")]
        [Tooltip("Trigger collider that defines where the player counts as 'too close to ignore'. " +
                 "Typically a wide BoxCollider2D on a child of the boss. CanRun returns true only " +
                 "when the player's position is inside this collider. Leave null to make the pattern " +
                 "always-eligible (legacy behavior).")]
        [SerializeField]
        private Collider2D punishZone;

        public override bool CanRun(VengefulSpiritPatternContext ctx) {
            Transform player = ctx.Player;
            if (player == null) {
                return false;
            }
            if (punishZone == null) {
                return true;
            }
            return punishZone.OverlapPoint(player.position);
        }

        public override IEnumerator Run(VengefulSpiritPatternContext ctx) {
            if (ctx.BossDead) {
                yield break;
            }
            // Lock facing onto the player at strike time — otherwise a punish that fires
            // after the boss already turned during the previous pattern would land in the
            // wrong direction.
            Transform player = ctx.Player;
            if (player != null) {
                ctx.Boss.FaceTowards(player.position.x);
            }
            ctx.Boss.RequestCommonAttack();
            yield return ctx.WaitForBusyCycle();
        }
    }
}
