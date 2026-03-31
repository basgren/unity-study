using Core.FSM;
using Game.Core.Bootstrap;
using Game.Core.Components.Damage;
using Prefabs.Characters.Common;
using UnityEngine;

namespace Game.Features.Characters.Parrot {
    public enum ParrotBehavior {
        Roaming,    // Free mode: ground patrol
        Following,  // Follow mode: fly near player, scan for enemies
        Attacking,  // Fly to enemy, deal damage
        Returning,  // Fly back to player after attack or timeout
        Landing,    // Brief settle before despawn
    }

    /// <summary>
    /// High-level parrot AI behavior coordinator.
    /// Owns a behavior FSM with five states:
    /// <list type="bullet">
    /// <item><description><c>Roaming</c>: walks back and forth on ground using patrol path or edge detection.</description></item>
    /// <item><description><c>Following</c>: flies near the player, scanning for enemies.</description></item>
    /// <item><description><c>Attacking</c>: flies to a detected enemy and performs up to <see cref="maxAttackCount"/> attacks.</description></item>
    /// <item><description><c>Returning</c>: flies back to the player after attacking or after follow timeout.</description></item>
    /// <item><description><c>Landing</c>: brief settle state before despawn / inventory return.</description></item>
    /// </list>
    /// </summary>
    [RequireComponent(typeof(ParrotController))]
    public class ParrotAI : MonoBehaviour {
        private sealed class ParrotBehaviorFsm : SimpleStateMachine<ParrotBehavior> {
            public ParrotBehaviorFsm(ParrotAI ai) : base(ParrotBehavior.Roaming) {
                PermitIf(ParrotBehavior.Roaming, ParrotBehavior.Following,
                    () => ai.ctrl.Mode == ParrotMode.Follow);

                PermitIf(ParrotBehavior.Following, ParrotBehavior.Attacking,
                    () => ai.HasValidTarget());
                PermitIf(ParrotBehavior.Following, ParrotBehavior.Returning,
                    () => ai.IsFollowTimedOut());

                PermitIf(ParrotBehavior.Attacking, ParrotBehavior.Returning,
                    () => ai.IsAttackSequenceDone());

                PermitIf(ParrotBehavior.Returning, ParrotBehavior.Landing,
                    () => ai.IsNearPlayer());

                // Landing has no outgoing FSM transition — recall is triggered
                // directly in Update after landingDuration elapses.
            }
        }

        [Header("Attack")]
        [SerializeField] private int maxAttackCount = 3;
        [SerializeField] private float attackInterval = 0.6f;

        [Header("Follow")]
        [SerializeField] private float followTimeout = 15f;
        [SerializeField] private float arrivalThreshold = 0.5f;
        [SerializeField] private float landingDuration = 0.3f;

        [Header("Patrol")]
        [SerializeField] private float edgeCheckDistance = 0.5f;
        [SerializeField] private float wallCheckDistance = 0.3f;
        [SerializeField] private LayerMask groundLayer;

        private ParrotController ctrl;
        private GroundPatrolPath path;
        private ParrotBehaviorFsm fsm;

        // Patrol state
        private int patrolDirection = 1;
        private float prevX;
        private float patrolWaitTimer;

        // Attack state
        private Damageable currentTarget;
        private int attacksDelivered;
        private float attackCooldownTimer;

        // Landing state — prevent double-recall
        private bool hasRecalled;

        public ParrotBehavior Behavior => fsm != null ? fsm.State : ParrotBehavior.Roaming;

        private void Awake() {
            ctrl = GetComponent<ParrotController>();
            path = GetComponent<GroundPatrolPath>();

            fsm = new ParrotBehaviorFsm(this);
            fsm.OnTransition += OnBehaviorTransition;
            G.StateMachines.Register(fsm, this);

            prevX = transform.position.x;
        }

        private void Update() {
            // If mode was switched to Free while in a Follow-related state, reset to Roaming
            if (ctrl.Mode == ParrotMode.Free && fsm.State != ParrotBehavior.Roaming) {
                ResetToRoaming();
                return;
            }

            switch (fsm.State) {
                case ParrotBehavior.Roaming:
                    UpdateRoaming();
                    break;
                case ParrotBehavior.Following:
                    UpdateFollowing();
                    break;
                case ParrotBehavior.Attacking:
                    UpdateAttacking();
                    break;
                case ParrotBehavior.Returning:
                    UpdateReturning();
                    break;
                case ParrotBehavior.Landing:
                    ctrl.Stop();
                    if (!hasRecalled && fsm.TimeInState >= landingDuration) {
                        hasRecalled = true;
                        ctrl.Recall();
                    }
                    break;
            }
        }

        // ---=== State Updates ===---

        private void UpdateRoaming() {
            if (path != null) {
                UpdatePatrolPath();
            } else {
                UpdateSimplePatrol();
            }
        }

        private void UpdateFollowing() {
            var player = GetPlayerTransform();
            if (player == null) {
                return;
            }

            var heroFacing = G.Hero.Controller.GetFacingDirSign();
            var targetPos = ctrl.GetSmoothedFollowPosition(player.position, heroFacing);
            ctrl.SmoothFlyToward(targetPos);

            // Scan for enemies while following
            if (currentTarget == null || currentTarget.IsDead) {
                currentTarget = ctrl.FindVisibleEnemy();
            }
        }

        private void UpdateAttacking() {
            // Target lost or dead — attack sequence is done, FSM will transition
            if (currentTarget == null || currentTarget.IsDead) {
                return;
            }

            var enemyPos = (Vector2)currentTarget.transform.position;

            if (ctrl.IsInDamagerRange(enemyPos)) {
                // Close enough — hold position and face the enemy
                ctrl.Stop();
                ctrl.SetFacing(enemyPos.x - transform.position.x);

                if (attackCooldownTimer > 0f) {
                    attackCooldownTimer -= Time.deltaTime;
                    return;
                }

                PerformAttack();
            } else {
                // Fly toward the enemy until within DamageTrigger range
                ctrl.FlyToward(enemyPos, ctrl.AttackFlySpeed);
            }
        }

        private void UpdateReturning() {
            var player = GetPlayerTransform();
            if (player == null) {
                return;
            }

            ctrl.FlyToward(player.position, ctrl.FlySpeed);
        }

        // ---=== Patrol Logic ===---

        private void UpdatePatrolPath() {
            if (path.Points == null || path.Points.Count == 0) {
                ctrl.SetMoveDirection(0);
                return;
            }

            if (patrolWaitTimer > 0f) {
                patrolWaitTimer -= Time.deltaTime;
                ctrl.SetMoveDirection(0);
                prevX = transform.position.x;
                return;
            }

            var point = path.GetTargetPoint();
            var targetX = point.position.x;

            if (ReachedOrPassed(targetX)) {
                if (point.delay > 0f) {
                    patrolWaitTimer = point.delay;
                }

                path.NextTarget();
                ctrl.SetMoveDirection(0);
                return;
            }

            var dir = targetX > transform.position.x ? 1 : -1;
            ctrl.SetMoveDirection(dir);
        }

        private void UpdateSimplePatrol() {
            // Raycast forward to check for walls
            var origin = (Vector2)transform.position;
            var forward = Vector2.right * patrolDirection;

            var wallHit = Physics2D.Raycast(origin, forward, wallCheckDistance, groundLayer);
            if (wallHit.collider != null) {
                patrolDirection = -patrolDirection;
            } else {
                // Raycast down-forward to check for ground edge
                var edgeOrigin = origin + forward * edgeCheckDistance;
                var edgeHit = Physics2D.Raycast(edgeOrigin, Vector2.down, edgeCheckDistance, groundLayer);
                if (edgeHit.collider == null) {
                    patrolDirection = -patrolDirection;
                }
            }

            ctrl.SetMoveDirection(patrolDirection);
        }

        private bool ReachedOrPassed(float targetX) {
            var x = transform.position.x;
            var crossed = (x - targetX) * (prevX - targetX) <= 0f;
            prevX = x;
            return crossed;
        }

        // ---=== Attack Sequence ===---

        private void PerformAttack() {
            ctrl.EnableDamager();
            ctrl.TriggerAttackAnimation();
            attacksDelivered++;
            attackCooldownTimer = attackInterval;

            // Damager is SingleHit — disable and re-enable on next attack pass
            // Schedule disable after a short frame so the trigger fires
            Invoke(nameof(DisableDamagerDelayed), 0.1f);
        }

        private void DisableDamagerDelayed() {
            ctrl.DisableDamager();
        }

        // ---=== Transition Conditions ===---

        private bool HasValidTarget() {
            return currentTarget != null && !currentTarget.IsDead;
        }

        private bool IsFollowTimedOut() {
            return fsm.TimeInState >= followTimeout;
        }

        private bool IsAttackSequenceDone() {
            if (currentTarget == null || currentTarget.IsDead) {
                return true;
            }

            return attacksDelivered >= maxAttackCount;
        }

        private bool IsNearPlayer() {
            var player = GetPlayerTransform();
            if (player == null) {
                return true;
            }

            return Vector2.Distance(transform.position, player.position) <= arrivalThreshold;
        }

        // ---=== Transition Handlers ===---

        private void OnBehaviorTransition(ParrotBehavior toState, ParrotBehavior fromState) {
            switch (toState) {
                case ParrotBehavior.Roaming:
                    ctrl.ApplyGravityForMode(ParrotMode.Free);
                    ctrl.SetCollectableActive(true);
                    ctrl.DisableDamager();
                    currentTarget = null;

                    patrolDirection = ctrl.GetFacingSign();
                    prevX = transform.position.x;
                    break;

                case ParrotBehavior.Following:
                    ctrl.ApplyGravityForMode(ParrotMode.Follow);
                    ctrl.SetCollectableActive(false);
                    ctrl.DisableDamager();
                    currentTarget = null;

                    break;

                case ParrotBehavior.Attacking:
                    attacksDelivered = 0;
                    attackCooldownTimer = 0f;
                    break;

                case ParrotBehavior.Returning:
                    ctrl.DisableDamager();
                    currentTarget = null;

                    break;

                case ParrotBehavior.Landing:
                    ctrl.Stop();
                    break;
            }
        }

        private void ResetToRoaming() {
            ctrl.DisableDamager();
            ctrl.Stop();
            currentTarget = null;
            ctrl.ApplyGravityForMode(ParrotMode.Free);
            ctrl.SetCollectableActive(true);
            fsm.ResetTo(ParrotBehavior.Roaming);
        }

        // ---=== Helpers ===---

        private Transform GetPlayerTransform() {
            var hero = G.Hero?.Controller;
            return hero != null ? hero.transform : null;
        }

        private void OnDrawGizmosSelected() {
            if (!Application.isPlaying) {
                return;
            }

            // Draw arrival threshold
            var player = GetPlayerTransform();
            if (player != null) {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(player.position, arrivalThreshold);
            }
        }
    }
}
