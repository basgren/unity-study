using System.Collections;
using Game.Core.Bootstrap;
using UnityEngine;

namespace Game.Features.Bosses.StoneGolem {
    /// <summary>
    /// Stone Golem encounter intro. The golem waits inert OFFSCREEN above the arena in its collapsed
    /// (ball) state, with gravity and its AI disabled. When the player picks up the fake skull —
    /// wire the pickup's UnityEvent to <see cref="Begin"/> — the golem slams down onto the arena
    /// floor, holds a beat, then rises out of the ball, the boss health bar engages, and the AI
    /// takes over. Replays are blocked after the first call.
    /// </summary>
    public class StoneGolemIntro : MonoBehaviour {
        [SerializeField]
        private StoneGolem golem;

        [SerializeField]
        [Tooltip("The golem's selector, held disabled until the intro hands off.")]
        private StoneGolemAI golemAI;

        [SerializeField]
        [Tooltip("Slam tuning for the intro drop: fall gravity, camera shake, impact sound.")]
        private StoneGolem.SlamSettings slamSettings = new StoneGolem.SlamSettings();

        [SerializeField]
        [Tooltip("Beat after landing before the golem rises out of the ball and the fight starts.")]
        private float revealHold = 1f;

        private bool started;

        // Set up in Start (not Awake) so the golem's own Awake has already run by now: it balls the
        // golem up (small collider, gravity zeroed), which also holds it offscreen until the intro fires.
        private void Start() {
            // Debug phase-2 start: StoneGolemAI brings the golem up itself (engages the boss, shatters
            // the floor, swaps the confiner). Don't hold it inert here or it would never activate.
            if (golemAI != null && golemAI.DebugStartInPhase2) {
                return;
            }

            if (golemAI != null) {
                golemAI.enabled = false;
            }
        }

        /// <summary>Intro entry point — wire the fake-skull pickup's UnityEvent here. No-ops after the first call.</summary>
        public void Begin() {
            if (started) {
                return;
            }

            started = true;
            StartCoroutine(IntroRoutine());
        }

        private IEnumerator IntroRoutine() {
            G.Hero.Controller.ShowConfusion();

            yield return new WaitForSeconds(2f);
            yield return golem.SlamToGround(slamSettings);

            if (revealHold > 0f) {
                yield return new WaitForSeconds(revealHold);
            }

            if (golem != null && golem.Damageable != null && G.BossFight != null) {
                G.BossFight.EngageBoss(golem.Damageable);
            }

            // Rise out of the ball, then hand control to the AI (its OnEnable resets the warmup timer,
            // so the player gets a beat before the first attack).
            golem.SetImmune(false);
            if (golemAI != null) {
                golemAI.enabled = true;
            }
        }
    }
}
