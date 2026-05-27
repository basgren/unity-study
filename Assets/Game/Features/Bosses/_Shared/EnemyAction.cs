using System;
using System.Collections;
using UnityEngine;

namespace Game.Features.Bosses._Shared {
    /// <summary>
    /// Base for a single discrete enemy/boss action with a fixed four-step lifecycle:
    /// <list type="number">
    /// <item><description><b>Start</b> — <see cref="Begin"/> arms state and plays the animation (<see cref="OnBegin"/>).</description></item>
    /// <item><description><b>Do</b> — <see cref="Do"/> performs the effect (open damage window, spawn projectile, cast).</description></item>
    /// <item><description><b>Tear down</b> — <see cref="TearDown"/> begins shutdown (close window, …).</description></item>
    /// <item><description><b>End</b> — <see cref="Complete"/> frees the boss (<see cref="OnEnd"/>, then <see cref="Completed"/>).</description></item>
    /// </list>
    /// The "Do" and "Tear down" steps are driven either by relayed animation events or by the
    /// action's own timing. The "End" step is code-owned (a timer / coroutine / callback) so a
    /// missing or misconfigured end animation event can never soft-lock the boss;
    /// <see cref="maxDuration"/> is a hard safety cap on top of that.
    /// </summary>
    public abstract class EnemyAction : MonoBehaviour {
        [SerializeField]
        [Tooltip("Hard safety cap in seconds. If the action has not completed by then it force-completes, " +
                 "so a missed end event or stuck coroutine cannot leave the boss permanently busy. " +
                 "Set to 0 to disable the safety cap.")]
        private float maxDuration = 5f;

        /// <summary>True between <see cref="Begin"/> and the moment the action ends.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Hard safety cap in seconds. Exposed so spawned visuals (e.g. beams) can tune their own lifetime against the action's.</summary>
        public float MaxDuration => maxDuration;

        /// <summary>Raised once when the action finishes naturally via <see cref="Complete"/> (not on <see cref="Cancel"/>).</summary>
        public event Action<EnemyAction> Completed;

        /// <summary>Step 1: start the action. No-op if already running.</summary>
        public void Begin() {
            if (IsRunning) {
                return;
            }

            IsRunning = true;
            OnBegin();
            StartCoroutine(SafetyTimeoutRoutine());
        }

        /// <summary>
        /// Step 2: perform the action's effect. Called by the host when the animation reaches its
        /// effect frame (relayed event) or by the action's own timing. Default is a no-op.
        /// </summary>
        public virtual void Do() {
        }

        /// <summary>
        /// Step 3: begin shutting the action down (e.g. close a damage window). Called by a relayed
        /// animation event or the action's own timing. Default is a no-op.
        /// </summary>
        public virtual void TearDown() {
        }

        /// <summary>
        /// Aborts the action immediately without raising <see cref="Completed"/>. Used for death and
        /// interrupt cleanup. Runs <see cref="OnEnd"/> so subclasses can release resources.
        /// </summary>
        public void Cancel() {
            if (!IsRunning) {
                return;
            }

            IsRunning = false;
            StopAllCoroutines();
            OnEnd();
        }

        /// <summary>Step 4: finish the action and notify the host. Safe to call more than once.</summary>
        protected void Complete() {
            if (!IsRunning) {
                return;
            }

            IsRunning = false;
            StopAllCoroutines();
            OnEnd();
            Completed?.Invoke(this);
        }

        /// <summary>Subclass hook: arm state and start the animation. Always called from <see cref="Begin"/>.</summary>
        protected abstract void OnBegin();

        /// <summary>Subclass hook: release resources / reset state. Called from both <see cref="Complete"/> and <see cref="Cancel"/>.</summary>
        protected virtual void OnEnd() {
        }

        private IEnumerator SafetyTimeoutRoutine() {
            if (maxDuration <= 0f) {
                yield break;
            }

            yield return new WaitForSeconds(maxDuration);
            Complete();
        }
    }
}
