using System.Collections;
using Game.Core.Components.Base2D;
using Game.Features.Bosses.VengefulSpirit.Teleport;
using Game.Features.Characters._Shared;
using UnityEngine;

namespace Game.Features.Bosses.VengefulSpirit.AI.Patterns {
    /// <summary>
    /// Blink to a position computed at runtime relative to the player. The
    /// <see cref="probe"/> child Transform on the boss defines an offset: after the blink,
    /// the probe ends up at the player's world position. The pattern prefers the spot
    /// behind the player (so the boss looks back at them) and only falls back to the front
    /// side if the back is blocked by world geometry. Boss always faces the player on
    /// reappearance, then strikes once and despawns to one of the configured exit anchors.
    ///
    /// Opts out of the AI's <c>followUpPattern</c> via <see cref="AllowFollowUp"/> — the
    /// strike is the punish, so chaining the AI's follow-up CommonAttack here would land
    /// a back-to-back double-hit on the player.
    /// </summary>
    public class BlinkAttackPattern : VengefulSpiritPattern {
        public override bool AllowFollowUp => false;

        [Header("Blink Attack")]
        [Tooltip("Telegraph delay between teleporting and the strike.")]
        [SerializeField]
        private float telegraphDelay = 0.5f;

        [Tooltip("Child Transform of the boss whose world position should land ON the player " +
                 "after the blink. The probe's local X gives the forward offset; its local Y " +
                 "gives the vertical offset. Drag a child object placed where the boss's 'reach " +
                 "point' lives in front of the body.")]
        [SerializeField]
        private Transform probe;

        [Tooltip("Layers treated as obstacles when checking whether a candidate blink position " +
                 "is free. Should include the level's ground / wall layers.")]
        [SerializeField]
        private LayerMask obstacleLayers;

        [Tooltip("Reference to the boss's body collider (typically the CapsuleCollider2D on the " +
                 "VengefulSpirit prefab). Its geometry is overlap-tested at the candidate blink " +
                 "position to validate the spot is free of obstacles. Leaving this null disables " +
                 "the check (the blink will always proceed).")]
        [SerializeField]
        private Collider2D bodyCollider;

        [Tooltip("Anchors the boss may relocate to after the strike — the despawn step. The " +
                 "pattern picks one at random (excluding the closest to the boss's current " +
                 "position so the move is visible). If empty, the boss stays at the strike spot " +
                 "and the next pattern handles repositioning.")]
        [SerializeField]
        private TeleportAnchor[] postBlinkRelocationAnchors;

        [Tooltip("Pause between the strike completing and the despawn fade starting. Lets the " +
                 "strike pose linger so the player can read the hit before the boss vanishes. " +
                 "0.5–1.0 reads well in playtest.")]
        [SerializeField]
        private float postStrikeDelay = 0.7f;

        public override bool CanRun(VengefulSpiritPatternContext ctx) {
            if (ctx.Player == null || probe == null) {
                return false;
            }
            // Only blink onto a grounded player. Mid-air blinks land the boss on whatever
            // X/Y the probe math produces, which can put it floating above/below platforms
            // when the player was jumping — looks broken. Falling back to the ground state
            // exposed by BaseCharacterController matches the player's own physics state
            // rather than a separate raycast on the boss side.
            BaseCharacterController controller = ctx.Player.GetComponent<BaseCharacterController>();
            if (controller != null && !controller.IsGrounded) {
                return false;
            }
            return true;
        }

        public override IEnumerator Run(VengefulSpiritPatternContext ctx) {
            Transform player = ctx.Player;
            if (player == null || probe == null) {
                yield break;
            }

            // Resolve player facing — Facing2D is the standard component; default to right
            // if the player's prefab doesn't carry one.
            Facing2D playerFacing = player.GetComponent<Facing2D>();
            int playerDirSign = playerFacing != null ? playerFacing.DirSign : 1;

            // Boss-faces-player in the preferred (behind) layout: boss appears on the
            // opposite side of the player, looking back at them. In world terms, "boss
            // appears behind player" means boss is on the OPPOSITE side from the player's
            // facing direction. With probe.localPosition.x positive (probe is forward of
            // boss), the math works out so bossFacingSign == playerDirSign (boss faces the
            // same world direction as the player, but is on the player's far side).
            FacingDir preferredFacing = playerDirSign >= 0 ? FacingDir.Right : FacingDir.Left;

            if (TryComputeBlinkPosition(player.position, preferredFacing, out Vector3 target)) {
                yield return DoBlink(ctx, target, preferredFacing);
                yield break;
            }

            // Preferred side blocked — flip to the player's facing side.
            FacingDir fallback = preferredFacing == FacingDir.Right ? FacingDir.Left : FacingDir.Right;
            if (TryComputeBlinkPosition(player.position, fallback, out target)) {
                yield return DoBlink(ctx, target, fallback);
                yield break;
            }

            // Both sides blocked — abort silently; the AI will pick another pattern.
        }

        private IEnumerator DoBlink(VengefulSpiritPatternContext ctx, Vector3 target, FacingDir bossFacing) {
            ctx.Boss.RequestTeleport(target, bossFacing);
            yield return ctx.WaitForBusyCycle();
            if (ctx.BossDead) {
                yield break;
            }

            float t = 0f;
            while (t < telegraphDelay) {
                if (ctx.BossDead) {
                    yield break;
                }
                t += Time.deltaTime;
                yield return null;
            }

            // Facing was committed at spawn time (via the teleporter's onAtDestination
            // callback). We deliberately do NOT re-aim at the player here — the spec is
            // "spawn → hit → despawn, no moves, no turns" and a re-aim during a strike
            // produces visual snapping that contradicts that.
            ctx.Boss.RequestCommonAttack();
            yield return ctx.WaitForBusyCycle();
            if (ctx.BossDead) {
                yield break;
            }

            // Hold the strike pose briefly before fading out so the hit reads clearly.
            float delayElapsed = 0f;
            while (delayElapsed < postStrikeDelay) {
                if (ctx.BossDead) {
                    yield break;
                }
                delayElapsed += Time.deltaTime;
                yield return null;
            }

            // Despawn step: teleport away from the strike spot. Keeps the blink self-
            // contained ("spawn → hit → despawn") and prevents the boss from lingering
            // next to the player while the AI picks the next pattern.
            TeleportAnchor exit = PickExitAnchor(ctx);
            if (exit != null) {
                ctx.Boss.RequestTeleport(exit);
                yield return ctx.WaitForBusyCycle();
            }
        }

        // Picks a random non-closest exit anchor — same shape as ChargeAttackPattern's
        // post-action relocation. Closest is skipped so the relocation is visible
        // (otherwise the boss could "blink" to nearly the same spot).
        private TeleportAnchor PickExitAnchor(VengefulSpiritPatternContext ctx) {
            if (postBlinkRelocationAnchors == null || postBlinkRelocationAnchors.Length == 0) {
                return null;
            }
            int closestIdx = -1;
            float closestSqr = float.PositiveInfinity;
            int validCount = 0;
            for (int i = 0; i < postBlinkRelocationAnchors.Length; i++) {
                TeleportAnchor a = postBlinkRelocationAnchors[i];
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
                return postBlinkRelocationAnchors[closestIdx];
            }
            int target = Random.Range(0, validCount - 1);
            int seen = 0;
            for (int i = 0; i < postBlinkRelocationAnchors.Length; i++) {
                TeleportAnchor a = postBlinkRelocationAnchors[i];
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

        // Solves: world(probe) == player.position, given the desired boss facing.
        // probe.localPosition.x flips with boss.localScale.x (driven by Facing2D), so we
        // apply the facing sign to the X offset only. Returns false if the resulting boss
        // position overlaps any obstacle (per the body collider's geometry).
        private bool TryComputeBlinkPosition(Vector3 playerPos, FacingDir bossFacing, out Vector3 bossTarget) {
            int sign = (int)bossFacing;
            Vector3 localProbe = probe.localPosition;
            Vector3 worldProbeOffset = new Vector3(sign * localProbe.x, localProbe.y, localProbe.z);
            bossTarget = playerPos - worldProbeOffset;
            return IsFreeSpaceAt(bossTarget, sign);
        }

        // Overlap-tests the boss's body collider at a candidate world position. Picks the
        // matching primitive overlap call so the test matches the collider's actual shape
        // (capsule/box/circle); falls back to a bounds-sized box for anything exotic. The
        // body collider's local offset is rotated by the requested facing sign so that an
        // off-center collider behaves correctly when the boss flips horizontally.
        private bool IsFreeSpaceAt(Vector2 candidate, int facingSign) {
            if (bodyCollider == null) {
                return true;
            }
            Vector2 localOffset = bodyCollider.offset;
            Vector2 center = candidate + new Vector2(facingSign * localOffset.x, localOffset.y);
            Collider2D blocker;
            switch (bodyCollider) {
                case CapsuleCollider2D capsule:
                    blocker = Physics2D.OverlapCapsule(center, capsule.size, capsule.direction, 0f, obstacleLayers);
                    break;
                case BoxCollider2D box:
                    blocker = Physics2D.OverlapBox(center, box.size, 0f, obstacleLayers);
                    break;
                case CircleCollider2D circle:
                    blocker = Physics2D.OverlapCircle(center, circle.radius, obstacleLayers);
                    break;
                default:
                    blocker = Physics2D.OverlapBox(center, bodyCollider.bounds.size, 0f, obstacleLayers);
                    break;
            }
            return blocker == null;
        }

#if UNITY_EDITOR
        // Highlight the probe so designers can tune its placement without entering play mode.
        // The body collider draws its own gizmo, so we don't redraw the free-space shape.
        private void OnDrawGizmosSelected() {
            if (probe == null) {
                return;
            }
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.8f);
            Gizmos.DrawWireSphere(probe.position, 0.2f);
        }
#endif
    }
}
