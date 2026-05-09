using System.Collections;
using Game.Features.Bosses.VengefulSpirit.Teleport;
using UnityEngine;

namespace Game.Features.Bosses.VengefulSpirit.AI.Patterns {
    /// <summary>
    /// Phase-2-only horizontal-dash attack. Boss teleports to one of two ground-level
    /// charge anchors, plays ChargeAttack1 (windup, no damage), then dashes horizontally
    /// to the other anchor with the dash damager active (ChargeAttack2). The action layer
    /// (<see cref="VengefulSpirit.RequestChargeAttack"/>) handles the windup-then-dash
    /// sequence; this pattern picks the start/end based on player position and finally
    /// teleports the boss to a different location after the idle.
    ///
    /// Owns its own anchor pair via inspector — there is no shared "charge anchor" list on
    /// the boss anymore.
    /// </summary>
    public class ChargeAttackPattern : VengefulSpiritPattern {
        [Header("Charge Attack")]
        [Tooltip("Anchor A. The pattern picks whichever of A/B is farther from the player as the dash start.")]
        [SerializeField]
        private TeleportAnchor anchorA;

        [SerializeField]
        private TeleportAnchor anchorB;

        [Tooltip("Anchors the boss may relocate to after the charge ends. Should not include " +
                 "the two charge anchors. If empty, the boss stays at the dash end position.")]
        [SerializeField]
        private TeleportAnchor[] postChargeRelocationAnchors;

        public override bool CanRun(VengefulSpiritPatternContext ctx) {
            return anchorA != null && anchorB != null;
        }

        public override IEnumerator Run(VengefulSpiritPatternContext ctx) {
            if (anchorA == null || anchorB == null) {
                yield break;
            }

            // Pick start (farther from the player) and end (closer to player) so the dash
            // visibly sweeps toward the player. Falls back to A->B with no player.
            Transform player = ctx.Player;
            TeleportAnchor from;
            TeleportAnchor to;
            if (player == null) {
                from = anchorA;
                to = anchorB;
            } else {
                float dA = Mathf.Abs(anchorA.transform.position.x - player.position.x);
                float dB = Mathf.Abs(anchorB.transform.position.x - player.position.x);
                if (dA >= dB) {
                    from = anchorA;
                    to = anchorB;
                } else {
                    from = anchorB;
                    to = anchorA;
                }
            }

            ctx.Boss.RequestChargeAttack(from, to);
            yield return ctx.WaitForBusyCycle();
            if (ctx.BossDead) {
                yield break;
            }

            // Spec: "Then boss disappears and teleports to a different location."
            TeleportAnchor away = PickRelocationAnchor(ctx);
            if (away != null) {
                ctx.Boss.RequestTeleport(away);
                yield return ctx.WaitForBusyCycle();
            }
        }

        private TeleportAnchor PickRelocationAnchor(VengefulSpiritPatternContext ctx) {
            if (postChargeRelocationAnchors == null || postChargeRelocationAnchors.Length == 0) {
                return null;
            }
            // Pick a non-null anchor that isn't the closest to the boss's current position
            // so the relocation is visible.
            int closestIdx = -1;
            float closestSqr = float.PositiveInfinity;
            int validCount = 0;
            for (int i = 0; i < postChargeRelocationAnchors.Length; i++) {
                TeleportAnchor a = postChargeRelocationAnchors[i];
                if (a == null) {
                    continue;
                }
                validCount++;
                float d = (a.transform.position - ctx.Boss.transform.position).sqrMagnitude;
                if (d < closestSqr) {
                    closestSqr = d;
                    closestIdx = i;
                }
            }
            if (validCount == 0) {
                return null;
            }
            if (validCount == 1) {
                return postChargeRelocationAnchors[closestIdx];
            }
            // Random non-closest pick.
            int target = Random.Range(0, validCount - 1);
            int seen = 0;
            for (int i = 0; i < postChargeRelocationAnchors.Length; i++) {
                TeleportAnchor a = postChargeRelocationAnchors[i];
                if (a == null || i == closestIdx) {
                    continue;
                }
                if (seen == target) {
                    return a;
                }
                seen++;
            }
            return null;
        }

#if UNITY_EDITOR
        // Draws the dash axis between the configured charge anchors so designers can
        // verify the pair at a glance — wiring the wrong anchors makes the boss dash
        // somewhere unintended without any error.
        private void OnDrawGizmosSelected() {
            if (anchorA == null || anchorB == null) {
                return;
            }
            Vector3 a = anchorA.transform.position;
            Vector3 b = anchorB.transform.position;
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.95f);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawWireSphere(a, 0.3f);
            Gizmos.DrawWireSphere(b, 0.3f);

            // Arrowheads at both ends — bidirectional because the pattern picks the start
            // dynamically based on player position.
            Vector3 dirAB = (b - a).normalized;
            DrawArrowhead(b, dirAB);
            DrawArrowhead(a, -dirAB);
        }

        private static void DrawArrowhead(Vector3 tip, Vector3 forward) {
            const float headSize = 0.35f;
            Vector3 perp = new Vector3(-forward.y, forward.x, 0f);
            Vector3 baseCenter = tip - forward * headSize;
            Gizmos.DrawLine(tip, baseCenter + perp * (headSize * 0.5f));
            Gizmos.DrawLine(tip, baseCenter - perp * (headSize * 0.5f));
        }
#endif
    }
}
