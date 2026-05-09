using System.Collections;
using Game.Core.Components.Collisions;
using Game.Features.Bosses.VengefulSpirit.Teleport;
using UnityEngine;

namespace Game.Features.Bosses.VengefulSpirit.AI.Patterns {
    /// <summary>
    /// Pursuit-style strategy. The boss spawns at one of the configured start anchors,
    /// then chases the player and tries to land <see cref="hitCount"/> common attacks.
    /// Each attack attempt counts toward the total whether or not it actually connects —
    /// the pattern doesn't care about damage delivery, only about staying readable
    /// (boss commits to N strikes, then leaves).
    ///
    /// Two ways the chase ends:
    /// - All <see cref="hitCount"/> attempts have been made.
    /// - Total chase time exceeds <see cref="maxChaseDuration"/> (boss can't catch the
    ///   player — give up so the cycle keeps moving).
    /// In both cases the boss despawns to one of <see cref="postChaseRelocationAnchors"/>.
    ///
    /// Opts out of the AI's follow-up via <see cref="AllowFollowUp"/> = false: the pattern
    /// already includes its own punishes, and the despawn typically lands the boss far
    /// from the player anyway.
    /// </summary>
    public class ChaseAttackPattern : VengefulSpiritPattern {
        public override bool AllowFollowUp => false;

        [Header("Chase Attack")]
        [Tooltip("Spawn anchors. The pattern teleports the boss to a random one (excluding " +
                 "the closest to the boss's current position so the spawn move is visible). " +
                 "Pattern is unrunnable until at least one anchor is wired.")]
        [SerializeField]
        private TeleportAnchor[] startAnchors;

        [Tooltip("Number of strike attempts before the chase ends. Misses count — the boss " +
                 "commits to N swings whether or not they land.")]
        [SerializeField]
        private int hitCount = 3;

        [Tooltip("Maximum total chase time (seconds) across all attempts. If this elapses " +
                 "before the boss has used all its strikes, it gives up and despawns.")]
        [SerializeField]
        private float maxChaseDuration = 5f;

        [Tooltip("Boss flight speed while chasing the player.")]
        [SerializeField]
        private float chaseSpeed = 5f;

        [Tooltip("Trigger collider on the boss (typically the AttackTrigger child) wrapped in " +
                 "a LayerCheck. The pattern commits to a strike whenever this reports colliding " +
                 "with the player layer — replaces a hand-tuned distance threshold with the " +
                 "actual reach of the attack collider.")]
        [SerializeField]
        private LayerCheck strikeTrigger;

        [Tooltip("Anchors the boss may relocate to after the chase ends — the despawn step. " +
                 "Pattern picks a random non-closest anchor so the move is visible. If empty, " +
                 "no despawn — the boss stays where the chase left it (typically not desirable).")]
        [SerializeField]
        private TeleportAnchor[] postChaseRelocationAnchors;

        [Tooltip("Pause between strikes — the boss holds in place after each hit before " +
                 "resuming the chase for the next attempt. Lets the strike read before the " +
                 "next lunge starts. Counts toward maxChaseDuration.")]
        [SerializeField]
        private float postStrikeChaseDelay = 0.3f;

        [Tooltip("Pause after the final strike (or timeout) before the despawn fade starts. " +
                 "Mirrors BlinkAttack's postStrikeDelay — gives the player a beat to react " +
                 "before the boss vanishes.")]
        [SerializeField]
        private float preDespawnDelay = 0.5f;

        public override bool CanRun(VengefulSpiritPatternContext ctx) {
            if (ctx.Player == null) {
                return false;
            }
            if (startAnchors == null || startAnchors.Length == 0) {
                return false;
            }
            if (strikeTrigger == null) {
                return false;
            }
            return true;
        }

        public override IEnumerator Run(VengefulSpiritPatternContext ctx) {
            // Spawn at a start anchor.
            TeleportAnchor start = PickRandomNonClosest(startAnchors, ctx.Boss.transform.position);
            if (start != null) {
                ctx.Boss.RequestTeleport(start);
                yield return ctx.WaitForBusyCycle();
            }
            if (ctx.BossDead) {
                yield break;
            }

            // Chase + strike loop.
            int hitsDone = 0;
            float startTime = Time.time;
            ctx.Boss.BeginChase();

            while (hitsDone < hitCount) {
                if (ctx.BossDead) {
                    break;
                }
                if (Time.time - startTime >= maxChaseDuration) {
                    break;
                }

                Transform player = ctx.Player;
                if (player == null) {
                    break;
                }

                Vector2 toPlayer = (Vector2)player.position - (Vector2)ctx.Boss.transform.position;
                if (strikeTrigger.IsColliding()) {
                    // Trigger overlapping the player layer — fire the strike WITHOUT leaving
                    // chase mode so the boss keeps its pursuit momentum through the swing
                    // (BeginAttack/UpdateAttack skip the velocity zero-out while isChasing).
                    // ChaseStep keeps ticking through the attack so the boss continues to
                    // track the player instead of coasting in a straight line.
                    ctx.Boss.ChaseStep(toPlayer, chaseSpeed);
                    ctx.Boss.RequestCommonAttack();
                    yield return TrackPlayerWhileBusy(ctx);
                    hitsDone++;
                    if (ctx.BossDead) {
                        break;
                    }
                    // Pause between strikes. EndChase + StopMovement so the boss holds in
                    // place during the wait; BeginChase resumes pursuit for the next hit.
                    // Skipped on the last strike since we're about to leave the loop.
                    if (hitsDone < hitCount && Time.time - startTime < maxChaseDuration) {
                        ctx.Boss.EndChase();
                        float pause = 0f;
                        while (pause < postStrikeChaseDelay) {
                            if (ctx.BossDead) {
                                break;
                            }
                            pause += Time.deltaTime;
                            yield return null;
                        }
                        if (ctx.BossDead) {
                            break;
                        }
                        ctx.Boss.BeginChase();
                    }
                } else {
                    ctx.Boss.ChaseStep(toPlayer, chaseSpeed);
                    yield return null;
                }
            }

            ctx.Boss.EndChase();
            if (ctx.BossDead) {
                yield break;
            }

            // Hold the final pose briefly before fading out so the last strike reads.
            float despawnHold = 0f;
            while (despawnHold < preDespawnDelay) {
                if (ctx.BossDead) {
                    yield break;
                }
                despawnHold += Time.deltaTime;
                yield return null;
            }

            // Despawn.
            TeleportAnchor exit = PickRandomNonClosest(postChaseRelocationAnchors, ctx.Boss.transform.position);
            if (exit != null) {
                ctx.Boss.RequestTeleport(exit);
                yield return ctx.WaitForBusyCycle();
            }
        }

        // Same shape as VengefulSpiritPatternContext.WaitForBusyCycle, but drives a fresh
        // ChaseStep each frame so the boss keeps tracking the player while the strike
        // plays out. Without this, the boss would coast on whatever velocity it had at
        // RequestCommonAttack time and a moving player would drift out from under the swing.
        private IEnumerator TrackPlayerWhileBusy(VengefulSpiritPatternContext ctx) {
            // Phase 1: wait for the boss to actually become busy (deadline-bounded so a
            // silently swallowed RequestCommonAttack doesn't soft-lock).
            float deadline = Time.time + 0.5f;
            while (!ctx.Boss.IsBusy && Time.time < deadline) {
                if (ctx.BossDead) {
                    yield break;
                }
                StepTowardPlayer(ctx);
                yield return null;
            }
            // Phase 2: chase the player through the strike.
            while (ctx.Boss.IsBusy) {
                if (ctx.BossDead) {
                    yield break;
                }
                StepTowardPlayer(ctx);
                yield return null;
            }
        }

        private void StepTowardPlayer(VengefulSpiritPatternContext ctx) {
            Transform player = ctx.Player;
            if (player == null) {
                return;
            }
            Vector2 toPlayer = (Vector2)player.position - (Vector2)ctx.Boss.transform.position;
            ctx.Boss.ChaseStep(toPlayer, chaseSpeed);
        }

        // Picks a random anchor that isn't the closest one to the given origin. Closest
        // is skipped so the teleport visibly moves the boss. Mirrors the helper in
        // BlinkAttackPattern / ChargeAttackPattern — duplicated because abstracting it
        // out would only save ~20 lines and add a shared utility class.
        private static TeleportAnchor PickRandomNonClosest(TeleportAnchor[] anchors, Vector3 origin) {
            if (anchors == null || anchors.Length == 0) {
                return null;
            }
            int closestIdx = -1;
            float closestSqr = float.PositiveInfinity;
            int validCount = 0;
            for (int i = 0; i < anchors.Length; i++) {
                if (anchors[i] == null) {
                    continue;
                }
                validCount++;
                float d = (anchors[i].transform.position - origin).sqrMagnitude;
                if (d < closestSqr) {
                    closestSqr = d;
                    closestIdx = i;
                }
            }
            if (validCount == 0) {
                return null;
            }
            if (validCount == 1) {
                return anchors[closestIdx];
            }
            int target = Random.Range(0, validCount - 1);
            int seen = 0;
            for (int i = 0; i < anchors.Length; i++) {
                if (anchors[i] == null || i == closestIdx) {
                    continue;
                }
                if (seen == target) {
                    return anchors[i];
                }
                seen++;
            }
            return null;
        }
    }
}
