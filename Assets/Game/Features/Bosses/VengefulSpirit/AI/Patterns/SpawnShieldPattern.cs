using System.Collections;
using Core.Audio;
using Game.Core.Bootstrap;
using Game.Features.Bosses.VengefulSpirit.Teleport;
using UnityEngine;

namespace Game.Features.Bosses.VengefulSpirit.AI.Patterns {
    /// <summary>
    /// Phase-2 opener cutscene. Plays as a scripted seven-step sequence:
    ///   1. Boss fades out and stays hidden for <see cref="longHiddenDuration"/>.
    ///   2. Reappears at <see cref="centerAnchor"/>.
    ///   3. Holds for <see cref="dramaticPause"/>.
    ///   4. Enters the casting pose; spawns a one-shot VFX prefab; plays the cast cue.
    ///   5. Holds the cast for <see cref="castHoldDuration"/>, then spawns the shield.
    ///   6. Lets the shield's spawn animation read for <see cref="postSpawnPause"/>.
    ///   7. Fades out in place. The boss stays invisible until the next pattern teleports
    ///      it back in (any pattern that begins with a teleport handles this naturally).
    /// The strategic layer invokes this pattern's <see cref="Run"/> directly when the HP
    /// threshold is crossed; the cycle picker excludes it via cap = 0 in both phases by
    /// default so it never spawns mid-cycle.
    /// </summary>
    public class SpawnShieldPattern : VengefulSpiritPattern {
        [Header("Spawn Shield")]
        [Tooltip("Anchor the boss is teleported to before spawning the shield. Typically a " +
                 "central position out of the player's reach. If null, the boss casts in place.")]
        [SerializeField]
        private TeleportAnchor centerAnchor;

        [Header("Cutscene Timings")]
        [Tooltip("How long the boss stays fully hidden during step 1 before reappearing at the central anchor.")]
        [SerializeField]
        private float longHiddenDuration = 2.5f;

        [Tooltip("Beat between reappearing and starting the casting pose (step 3).")]
        [SerializeField]
        private float dramaticPause = 1.5f;

        [Tooltip("How long the boss holds the casting pose before the shield is instantiated (step 5).")]
        [SerializeField]
        private float castHoldDuration = 2.5f;

        [Tooltip("Beat between the shield spawning and the boss fading out (step 6).")]
        [SerializeField]
        private float postSpawnPause = 1f;

        [Header("Cutscene VFX / SFX")]
        [Tooltip("One-shot particle prefab spawned at the boss's position when the casting " +
                 "pose begins. Should self-destroy (or carry its own lifetime helper).")]
        [SerializeField]
        private GameObject castParticlesPrefab;

        [Tooltip("Audio cue played once when the casting pose begins.")]
        [SerializeField]
        private AudioCue castStartCue;

        public override bool CanRun(VengefulSpiritPatternContext ctx) {
            return !ctx.Boss.HasActiveShield;
        }

        public override IEnumerator Run(VengefulSpiritPatternContext ctx) {
            // Steps 1+2: fade out, hold hidden longer than the default, reappear at center.
            if (centerAnchor != null) {
                ctx.Boss.RequestTeleport(centerAnchor, longHiddenDuration);
                yield return ctx.WaitForBusyCycle();
                if (ctx.BossDead) {
                    yield break;
                }
            }

            // Step 3: dramatic pause.
            yield return WaitWithDeathCheck(ctx, dramaticPause);
            if (ctx.BossDead) {
                yield break;
            }

            // Step 4: enter casting pose, fire VFX + SFX.
            ctx.Boss.RequestBeginShieldCast();
            if (castParticlesPrefab != null) {
                Instantiate(castParticlesPrefab, ctx.Boss.transform.position, Quaternion.identity);
            }
            if (castStartCue != null && G.Audio != null) {
                G.Audio.Play2D(castStartCue);
            }

            // Step 5: hold casting, then spawn the shield.
            yield return WaitWithDeathCheck(ctx, castHoldDuration);
            if (ctx.BossDead) {
                yield break;
            }
            ctx.Boss.RequestFinishShieldCastAndSpawn();

            // Step 6: let the shield's spawn animation read.
            yield return WaitWithDeathCheck(ctx, postSpawnPause);
            if (ctx.BossDead) {
                yield break;
            }

            // Step 7: fade out in place.
            ctx.Boss.RequestFadeOutInPlace();
            yield return ctx.WaitForBusyCycle();
        }

        private static IEnumerator WaitWithDeathCheck(VengefulSpiritPatternContext ctx, float seconds) {
            float t = 0f;
            while (t < seconds) {
                if (ctx.BossDead) {
                    yield break;
                }
                t += Time.deltaTime;
                yield return null;
            }
        }
    }
}
