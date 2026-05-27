using System.Collections.Generic;
using Game.Features.Bosses._Shared;
using UnityEngine;

namespace Game.Features.Bosses.StoneGolem {
    /// <summary>
    /// Debug-only helper. Add to the golem root to get a play-mode button panel (drawn by its
    /// custom editor) that fires each <see cref="EnemyAction"/> found in children and drives
    /// movement directly. Any debug command first suspends <see cref="StoneGolemAI"/> so manual
    /// control does not fight the selector; press "Resume AI" to hand control back.
    /// </summary>
    [RequireComponent(typeof(StoneGolem))]
    public class StoneGolemDebugControls : MonoBehaviour {
        [SerializeField]
        [Tooltip("Horizontal target distance for the Move Left / Move Right buttons. Movement is " +
                 "continuous until Stop, so this only needs to be far enough to pick a direction.")]
        private float moveTargetDistance = 5f;

        [SerializeField]
        [Tooltip("Initial control mode at scene start. Checked: AI runs autonomously. Unchecked: " +
                 "AI starts disabled and the boss waits for manual debug input (use Resume AI to " +
                 "hand control back). Runtime toggling has no effect — applied once in Start.")]
        private bool startWithAi = true;

        [SerializeField]
        [Tooltip("When enabled, logs every step of the boss action lifecycle (start / do / tear down / end).")]
        private bool enableLogging;

        private StoneGolem golem;
        private StoneGolemAI ai;
        private readonly List<EnemyAction> actions = new List<EnemyAction>();

        /// <summary>The hosted golem controller (resolved lazily).</summary>
        public StoneGolem Golem {
            get {
                if (golem == null) {
                    golem = GetComponent<StoneGolem>();
                }

                return golem;
            }
        }

        /// <summary>True when the golem can start an action right now (free and alive).</summary>
        public bool CanAct => Golem != null && !Golem.IsBusy && !Golem.IsDead;

        /// <summary>True while the selector is active. Mirrors <c>StoneGolemAI.enabled</c>.</summary>
        public bool AiEnabled => Ai != null && Ai.enabled;

        private StoneGolemAI Ai {
            get {
                if (ai == null) {
                    ai = GetComponent<StoneGolemAI>();
                }

                return ai;
            }
        }

        private void OnEnable() {
            // Always subscribe; the enableLogging flag is checked inside the handlers so the
            // inspector toggle takes effect immediately without re-subscribing.
            StoneGolem g = Golem;
            if (g != null) {
                g.ActionStarted += OnActionStarted;
                g.ActionDo += OnActionDo;
                g.ActionTearDown += OnActionTearDown;
                g.ActionEnded += OnActionEnded;
            }
        }

        private void Start() {
            // Apply the initial control mode once. Both Awakes and OnEnables have run by now, so
            // toggling Ai.enabled here is deterministic — no Update has fired yet.
            if (Ai != null) {
                // The phase-2 debug start (on StoneGolemAI) owns activation — it engages the boss and
                // breaks the floor and must run, so don't force it to manual here even if startWithAi
                // is off; that combination would otherwise leave the golem standing inert.
                if (Ai.DebugStartInPhase2) {
                    Ai.enabled = true;
                    return;
                }

                Ai.enabled = startWithAi;
            }
        }

        private void OnDisable() {
            StoneGolem g = Golem;
            if (g != null) {
                g.ActionStarted -= OnActionStarted;
                g.ActionDo -= OnActionDo;
                g.ActionTearDown -= OnActionTearDown;
                g.ActionEnded -= OnActionEnded;
            }
        }

        private void OnActionStarted(EnemyAction action) {
            LogStep(action, "started");
        }

        private void OnActionDo(EnemyAction action) {
            LogStep(action, "do");
        }

        private void OnActionTearDown(EnemyAction action) {
            LogStep(action, "tear down");
        }

        private void OnActionEnded(EnemyAction action) {
            LogStep(action, "ended");
        }

        private void LogStep(EnemyAction action, string step) {
            if (!enableLogging || action == null) {
                return;
            }

            // Second arg makes the log entry clickable to ping the action GameObject.
            Debug.Log($"[StoneGolem] {action.gameObject.name} · {step} (t={Time.time:F2})", action);
        }

        /// <summary>Re-scans children for action components. Returns the live list (do not mutate).</summary>
        public IReadOnlyList<EnemyAction> RefreshActions() {
            actions.Clear();
            GetComponentsInChildren(true, actions);
            return actions;
        }

        /// <summary>Fires the given action, taking manual control from the selector first.</summary>
        public void Run(EnemyAction action) {
            TakeControl();
            if (Golem != null) {
                Golem.RunAction(action);
            }
        }

        /// <summary>Starts moving left (continuous until <see cref="Stop"/>).</summary>
        public void MoveLeft() {
            TakeControl();
            if (Golem != null) {
                Golem.MoveTowards(transform.position.x - moveTargetDistance);
            }
        }

        /// <summary>Starts moving right (continuous until <see cref="Stop"/>).</summary>
        public void MoveRight() {
            TakeControl();
            if (Golem != null) {
                Golem.MoveTowards(transform.position.x + moveTargetDistance);
            }
        }

        /// <summary>Stops horizontal movement.</summary>
        public void Stop() {
            TakeControl();
            if (Golem != null) {
                Golem.StopMoving();
            }
        }

        /// <summary>Re-enables the selector so the golem resumes autonomous behavior.</summary>
        public void EnableAi() {
            if (Ai != null) {
                Ai.enabled = true;
            }
        }

        // Suspends the selector so manual commands are not immediately overridden by it.
        private void TakeControl() {
            if (Ai != null && Ai.enabled) {
                Ai.enabled = false;
            }
        }
    }
}
