using System;
using System.Reflection;
using Core.FSM;
using Core.Services;
using Prefabs.Characters.Common;
using UnityEngine;

namespace Prefabs.Characters.PinkStar {
    public enum PinkyBehavior {
        Roaming,     // default: patrol, low attention
        Hunting,     // alert/aware: actively tries to engage player
        Recovering,  // post-attack / post-hit: lower aggressiveness but still not calm
    }

    /// <summary>
    /// High-level Pinky AI behavior coordinator.
    /// Owns a behavior FSM with three states:
    /// <list type="bullet">
    /// <item><description><c>Roaming</c>: patrols on <see cref="GroundPatrolPath"/> points and delays.</description></item>
    /// <item><description><c>Hunting</c>: chases/engages player and does not fall back to patrol direction.</description></item>
    /// <item><description><c>Recovering</c>: short look-around phase before returning to patrol.</description></item>
    /// </list>
    ///
    /// Core behavior rules:
    /// <list type="bullet">
    /// <item><description>Roaming follows patrol path movement only.</description></item>
    /// <item><description>Hunting moves toward currently visible player, otherwise keeps the last seen direction.</description></item>
    /// <item><description>On hit, AI immediately enters Hunting and forces movement toward hit-source direction for <c>hitDrivenHuntDuration</c> (default 2s).</description></item>
    /// <item><description>When hit-driven hunting ends without spotting player, transitions to Recovering.</description></item>
    /// <item><description>After Recovering duration/turn logic ends, transitions back to Roaming unless player is re-detected.</description></item>
    /// </list>
    ///
    /// Tactical state interaction:
    /// this class emits movement/attack intents; <see cref="PinkyController"/> and <see cref="PinkyStateMachine"/>
    /// apply tactical restrictions (anticipation/attack/cooldown/hit flow).
    /// </summary>
    [RequireComponent(typeof(PinkyController))]
    public class PinkyAI : PinkyControlSource {
        private sealed class PinkyBehaviorStateMachine : SimpleStateMachine<PinkyBehavior> {
            private readonly PinkyAI ai;

            public PinkyBehaviorStateMachine(PinkyAI ai) : base(PinkyBehavior.Roaming) {
                this.ai = ai;

                AddTransition(PinkyBehavior.Roaming, PinkyBehavior.Recovering)
                    .SetCondition(_ => ai.HasPendingRecoverRequest());
                AddTransition(PinkyBehavior.Roaming, PinkyBehavior.Hunting)
                    .SetCondition(_ => !ai.HasPendingRecoverRequest() && ai.ShouldEnterHuntingFromRoaming());
                AddTransition(PinkyBehavior.Hunting, PinkyBehavior.Recovering)
                    .SetCondition(_ => ai.ShouldEnterRecoveringFromHunting());
                AddTransition(PinkyBehavior.Recovering, PinkyBehavior.Hunting)
                    .SetCondition(_ => ai.ShouldEnterHuntingFromRecovering());
                AddTransition(PinkyBehavior.Recovering, PinkyBehavior.Roaming)
                    .SetCondition(_ =>
                        !ai.ShouldEnterHuntingFromRecovering() &&
                        ai.ShouldReturnToRoamingFromRecovering());
            }
        }

        [Header("Hunting")]
        [SerializeField]
        private float awareLoseTime = 3f;

        [SerializeField]
        private bool keepDirectionWhileBusy;

        [SerializeField]
        private float hitDrivenHuntDuration = 2f;

        [Header("Recovering")]
        [SerializeField]
        private float recoverDurationMin = 4f;

        [SerializeField]
        private float recoverDurationMax = 5f;

        [SerializeField]
        private float lookDuration = 0.5f;

        [SerializeField]
        [Min(0)]
        private int recoverTurnCount = 2;

        private IPinkySensors sensors;
        private PinkyController ctrl;
        private GroundPatrolPath path;

        private PinkyBehaviorStateMachine behaviorStateMachine;
        private float prevX;
        private float patrolWaitTimer;
        private float recoverStateDuration;
        private float nextRecoverTurnTime;
        private float lastSeenTime = float.NegativeInfinity;

        private int lastSeenDir = 1;
        private int recoverTurnsDone;
        private int recoverLookDirection = 1;

        private bool hasObservedCooldownInHunt;
        private int pendingRecoverTurnDirection;
        private bool pendingRecoverRequest;
        private int pendingRecoverSourceDirection;
        private bool hitDrivenHuntActive;
        private float hitDrivenHuntEndTime;
        private int hitDrivenHuntDirection;
        private bool hitDrivenHuntSawPlayer;

        private GameObject currentTarget;
        private PinkyStateMachine cachedTacticalStateMachine;
        private Func<PinkyState> tacticalStateGetter;

        private GameObject hero;

        public PinkyBehavior Behavior {
            get {
                return behaviorStateMachine != null
                    ? behaviorStateMachine.State
                    : PinkyBehavior.Roaming;
            }
        }

        private void Awake() {
            hero = GameObject.FindGameObjectWithTag("Player");
            if (hero == null) {
                throw new Exception("Could not find player object!");
            }
            
            ctrl = GetComponent<PinkyController>();
            sensors = ctrl;
            path = GetComponent<GroundPatrolPath>();
            behaviorStateMachine = new PinkyBehaviorStateMachine(this);
            behaviorStateMachine.OnStateEnter += OnBehaviorStateEnter;
            G.StateMachines.Register(behaviorStateMachine, this);

            prevX = transform.position.x;
            lastSeenDir = ctrl.GetCurrentDirection();
            tacticalStateGetter = GetTacticalStateFallback;
        }

        private void OnEnable() {
            behaviorStateMachine?.ResetTo(PinkyBehavior.Roaming);
            patrolWaitTimer = 0f;
            recoverStateDuration = 0f;
            nextRecoverTurnTime = 0f;
            recoverTurnsDone = 0;
            recoverLookDirection = ctrl.GetCurrentDirection();
            hasObservedCooldownInHunt = false;
            pendingRecoverTurnDirection = 0;
            pendingRecoverRequest = false;
            pendingRecoverSourceDirection = 0;
            hitDrivenHuntActive = false;
            hitDrivenHuntEndTime = 0f;
            hitDrivenHuntDirection = 0;
            hitDrivenHuntSawPlayer = false;
            currentTarget = null;
            lastSeenTime = float.NegativeInfinity;
            prevX = transform.position.x;
            lastSeenDir = ctrl.GetCurrentDirection();
        }

        public override PinkyCommand? GetCommand() {
            EnsureReferences();

            if (ctrl == null || sensors == null || ctrl.IsDead) {
                return CreateCommand(0, false);
            }

            UpdateTargetAwareness();
            behaviorStateMachine.Update(Time.deltaTime);

            switch (behaviorStateMachine.State) {
                case PinkyBehavior.Hunting:
                    return UpdateHunting();

                case PinkyBehavior.Recovering:
                    return UpdateRecovering();

                default:
                    return UpdateRoaming();
            }
        }

        public void OnHeroInVision(GameObject hero) {
            if (!enabled || ctrl == null || ctrl.IsDead) {
                return;
            }

            currentTarget = hero;
            RememberTargetDirection();
        }

        public void OnHeroInAttackTrigger(GameObject hero) {
            if (!enabled || ctrl == null || ctrl.IsDead) {
                return;
            }

            currentTarget = hero;
            RememberTargetDirection();
        }

        public void OnHitReceived(int hitSourceDirection) {
            EnsureReferences();

            if (!enabled || ctrl == null || ctrl.IsDead) {
                return;
            }

            hitDrivenHuntDirection = ClampDirection(hitSourceDirection);
            if (hitDrivenHuntDirection == 0) {
                hitDrivenHuntDirection = GetTargetDirection();
            }

            hitDrivenHuntActive = true;
            hitDrivenHuntEndTime = Time.time + Mathf.Max(0f, hitDrivenHuntDuration);
            hitDrivenHuntSawPlayer = IsPlayerInSight();
            pendingRecoverSourceDirection = hitDrivenHuntDirection;
            pendingRecoverRequest = false;

            if (behaviorStateMachine == null) {
                return;
            }

            behaviorStateMachine.Go(PinkyBehavior.Hunting);
        }

        private PinkyCommand UpdateRoaming() {
            return CreateCommand(GetPatrolDirection(), false);
        }

        private PinkyCommand UpdateHunting() {
            var tacticalState = GetTacticalState();
            var forceHitDrivenMovement = IsHitDrivenHuntInProgress();
            var moveDirection = forceHitDrivenMovement ? GetHitDrivenHuntDirection() : GetTargetDirection();

            if (tacticalState == PinkyState.Cooldown) {
                hasObservedCooldownInHunt = true;
            }

            if (IsAttackOpportunityTriggered()) {
                currentTarget = hero;
                RememberTargetDirection();
            }

            if (tacticalState != PinkyState.Calm) {
                return CreateCommand((forceHitDrivenMovement || keepDirectionWhileBusy) ? moveDirection : 0, false);
            }

            if (sensors.IsGrounded() && IsAttackOpportunityTriggered()) {
                return CreateCommand(moveDirection, true);
            }

            return CreateCommand(moveDirection, false);
        }

        private PinkyCommand UpdateRecovering() {
            if (pendingRecoverTurnDirection != 0) {
                var turnDir = pendingRecoverTurnDirection;
                pendingRecoverTurnDirection = 0;
                return CreateCommand(turnDir, false);
            }

            if (recoverTurnsDone < recoverTurnCount && Time.time >= nextRecoverTurnTime) {
                recoverLookDirection = -recoverLookDirection;
                if (recoverLookDirection != 0) {
                    lastSeenDir = recoverLookDirection;
                }

                recoverTurnsDone++;
                nextRecoverTurnTime = Time.time + Mathf.Max(0.01f, lookDuration);
                return CreateCommand(recoverLookDirection, false);
            }

            return CreateCommand(0, false);
        }

        private void EnsureReferences() {
            if (ctrl == null) {
                ctrl = GetComponent<PinkyController>();
            }

            if (sensors == null) {
                sensors = ctrl;
            }

            if (path == null) {
                path = GetComponent<GroundPatrolPath>();
            }

            if (cachedTacticalStateMachine == null) {
                TryBindTacticalState();
            }
        }

        private void TryBindTacticalState() {
            if (ctrl == null) {
                return;
            }

            var stateField = typeof(PinkyController).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic);
            cachedTacticalStateMachine = stateField?.GetValue(ctrl) as PinkyStateMachine;

            if (cachedTacticalStateMachine != null) {
                tacticalStateGetter = () => cachedTacticalStateMachine.State;
            }
        }

        private PinkyState GetTacticalState() {
            if (cachedTacticalStateMachine == null) {
                TryBindTacticalState();
            }

            return tacticalStateGetter();
        }

        private PinkyState GetTacticalStateFallback() {
            // TODO: Replace this fallback with an explicit tactical-state accessor if PinkyController changes.
            return cachedTacticalStateMachine != null ? cachedTacticalStateMachine.State : PinkyState.Calm;
        }

        private void UpdateTargetAwareness() {
            if (IsPlayerInSight() || IsAttackOpportunityTriggered()) {
                currentTarget = hero;
                RememberTargetDirection();
                if (hitDrivenHuntActive) {
                    hitDrivenHuntSawPlayer = true;
                }
            }
        }

        private bool HasPendingRecoverRequest() {
            return pendingRecoverRequest;
        }

        private bool ShouldEnterHuntingFromRoaming() {
            return IsPlayerInSight();
        }

        private bool ShouldEnterRecoveringFromHunting() {
            if (HasPendingRecoverRequest()) {
                return true;
            }

            if (hitDrivenHuntActive) {
                if (IsHitDrivenHuntInProgress()) {
                    return false;
                }

                var shouldRecover = !hitDrivenHuntSawPlayer;
                hitDrivenHuntActive = false;
                hitDrivenHuntSawPlayer = false;

                if (shouldRecover) {
                    pendingRecoverRequest = true;
                    pendingRecoverSourceDirection = GetHitDrivenHuntDirection();
                    return true;
                }
            }

            var tacticalState = GetTacticalState();

            if (tacticalState == PinkyState.Cooldown) {
                hasObservedCooldownInHunt = true;
                return false;
            }

            if (hasObservedCooldownInHunt && tacticalState == PinkyState.Calm) {
                return true;
            }

            if (awareLoseTime > 0f && Time.time - lastSeenTime > awareLoseTime) {
                return true;
            }

            return false;
        }

        private bool ShouldEnterHuntingFromRecovering() {
            return IsAttackOpportunityTriggered();
        }

        private bool ShouldReturnToRoamingFromRecovering() {
            return behaviorStateMachine.TimeInState >= recoverStateDuration;
        }

        private void OnBehaviorStateEnter(PinkyBehavior nextState, PinkyBehavior prevState) {
            switch (nextState) {
                case PinkyBehavior.Hunting:
                    hasObservedCooldownInHunt = false;
                    prevX = transform.position.x;
                    currentTarget = hero;
                    RememberTargetDirection();
                    break;

                case PinkyBehavior.Recovering:
                    ConfigureRecoveringState();
                    break;

                case PinkyBehavior.Roaming:
                    patrolWaitTimer = 0f;
                    break;
            }
        }

        private void ConfigureRecoveringState() {
            hasObservedCooldownInHunt = false;
            prevX = transform.position.x;
            recoverStateDuration = UnityEngine.Random.Range(
                Mathf.Min(recoverDurationMin, recoverDurationMax),
                Mathf.Max(recoverDurationMin, recoverDurationMax)
            );
            recoverTurnsDone = 0;
            nextRecoverTurnTime = Time.time + Mathf.Max(0.01f, lookDuration);

            var turnDirection = pendingRecoverRequest ? pendingRecoverSourceDirection : 0;
            if (turnDirection == 0) {
                turnDirection = GetTargetDirection();
            }

            if (turnDirection != 0) {
                lastSeenDir = turnDirection;
                recoverLookDirection = turnDirection;
                pendingRecoverTurnDirection = turnDirection;
            } else {
                recoverLookDirection = ctrl.GetCurrentDirection();
                pendingRecoverTurnDirection = 0;
            }

            pendingRecoverRequest = false;
            pendingRecoverSourceDirection = 0;
        }

        private bool IsHitDrivenHuntInProgress() {
            return hitDrivenHuntActive && Time.time < hitDrivenHuntEndTime;
        }

        private int GetHitDrivenHuntDirection() {
            if (hitDrivenHuntDirection != 0) {
                return hitDrivenHuntDirection;
            }

            if (lastSeenDir != 0) {
                return lastSeenDir;
            }

            return ctrl != null ? ctrl.GetCurrentDirection() : 0;
        }

        private void RememberTargetDirection() {
            lastSeenDir = GetTargetDirection();
            lastSeenTime = Time.time;
        }

        private int GetTargetDirection() {
            var hasSensorContact = IsPlayerInSight() || IsAttackOpportunityTriggered();
            if (currentTarget != null && hasSensorContact) {
                var deltaX = currentTarget.transform.position.x - transform.position.x;
                var targetDir = ClampDirection(Mathf.RoundToInt(Mathf.Sign(deltaX)));

                if (targetDir != 0) {
                    return targetDir;
                }
            }

            if (lastSeenDir != 0) {
                return lastSeenDir;
            }

            return ctrl.GetCurrentDirection();
        }

        private int GetPatrolDirection() {
            if (path == null || path.Points == null || path.Points.Count == 0) {
                prevX = transform.position.x;
                return 0;
            }

            if (patrolWaitTimer > 0f) {
                patrolWaitTimer -= Time.deltaTime;
                prevX = transform.position.x;
                return 0;
            }

            var point = path.GetTargetPoint();
            var targetX = point.position.x;

            if (ReachedOrPassed(targetX)) {
                if (point.delay > 0f) {
                    patrolWaitTimer = point.delay;
                }

                path.NextTarget();
                return 0;
            }

            return GetDirectionTowards(targetX);
        }

        private bool ReachedOrPassed(float targetX) {
            var x = transform.position.x;
            var crossed = (x - targetX) * (prevX - targetX) <= 0f;
            prevX = x;
            return crossed;
        }

        private int GetDirectionTowards(float targetX) {
            var deltaX = targetX - transform.position.x;
            return ClampDirection(Mathf.RoundToInt(Mathf.Sign(deltaX)));
        }

        private bool IsPlayerInSight() {
            return sensors != null && sensors.IsPlayerInSight();
        }

        private bool IsAttackOpportunityTriggered() {
            return IsPlayerInSight();
        }

        private static int ClampDirection(int direction) {
            if (direction > 0) {
                return 1;
            }

            if (direction < 0) {
                return -1;
            }

            return 0;
        }

        private static PinkyCommand CreateCommand(int xDirection, bool attack) {
            return new PinkyCommand(ClampDirection(xDirection), attack);
        }
    }
}
