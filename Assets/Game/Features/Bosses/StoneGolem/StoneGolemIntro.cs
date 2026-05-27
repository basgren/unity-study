using System.Collections;
using Game.Core.Bootstrap;
using UnityEngine;

namespace Game.Features.Bosses.StoneGolem {
    /// <summary>
    /// Stone Golem encounter intro. The golem waits inert OFFSCREEN above the arena in its collapsed
    /// ("immune") state, with gravity and its AI disabled. When the player picks up the fake skull —
    /// wire the pickup's UnityEvent to <see cref="Begin"/> — the golem drops in to
    /// <see cref="landingPoint"/>, holds a beat, then rises out of the ball, the boss health bar
    /// engages, and the AI takes over. Replays are blocked after the first call.
    /// </summary>
    public class StoneGolemIntro : MonoBehaviour {
        [SerializeField]
        private StoneGolem golem;

        [SerializeField]
        [Tooltip("The golem's selector, held disabled until the intro hands off.")]
        private StoneGolemAI golemAI;

        [SerializeField]
        [Tooltip("Golem animator; the intro drives the immune (ball) bool.")]
        private Animator animator;

        [SerializeField]
        private string animBoolKey = "isImmune";

        [SerializeField]
        [Tooltip("Where the golem lands when it drops in. Place it on the arena floor under the " +
                 "golem's offscreen start position.")]
        private Transform landingPoint;

        [SerializeField]
        [Tooltip("Descent time from the offscreen start down to the landing point.")]
        private float fallTime = 0.8f;

        [SerializeField]
        [Tooltip("Beat after landing before the golem rises out of the ball and the fight starts.")]
        private float revealHold = 1f;

        private int animBoolHash;
        private bool started;

        private void Awake() {
            animBoolHash = Animator.StringToHash(animBoolKey);
        }

        // Set up in Start (not Awake) so the golem's own Awake has already run by now.
        private void Start() {
            // Debug phase-2 start: StoneGolemAI brings the golem up itself (engages the boss, shatters
            // the floor, swaps the confiner). Don't hold it inert here or it would never activate.
            if (golemAI != null && golemAI.DebugStartInPhase2) {
                return;
            }

            if (golemAI != null) {
                golemAI.enabled = false;
            }

            SetImmune(true);

            // Hold the golem offscreen until the intro fires, or gravity would drop it on load.
            if (golem != null) {
                golem.SetGravityActive(false);
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
            
            // Drop in. FlyTo restores gravity on arrival, so the golem rests on the floor.
            if (golem != null && landingPoint != null) {
                yield return golem.FlyTo(landingPoint.position, fallTime);
            } else if (golem != null) {
                // No landing point wired — just let it fall under its own gravity.
                golem.SetGravityActive(true);
            }

            if (revealHold > 0f) {
                yield return new WaitForSeconds(revealHold);
            }

            if (golem != null && golem.Damageable != null && G.BossFight != null) {
                G.BossFight.EngageBoss(golem.Damageable);
            }

            // Rise out of the ball, then hand control to the AI (its OnEnable resets the warmup timer,
            // so the player gets a beat before the first attack).
            SetImmune(false);
            if (golemAI != null) {
                golemAI.enabled = true;
            }
        }

        private void SetImmune(bool value) {
            if (animator != null) {
                animator.SetBool(animBoolHash, value);
            }
        }
    }
}
