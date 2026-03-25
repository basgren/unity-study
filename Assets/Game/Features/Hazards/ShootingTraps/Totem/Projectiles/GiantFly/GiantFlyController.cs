using Core.Audio;
using Core.Components.Animation;
using Core.Components.Base2D;
using Core.Components.Damage;
using Core.FSM;
using Core.Services;
using Core.Utils;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace Prefabs.Hazards.ShootingTraps.Totem.Projectiles.GiantFly {
    [RequireComponent(typeof(MultiStateSpriteAnimator), typeof(Rigidbody2D), typeof(Facing2D))]
    public class GiantFlyController : MonoBehaviour {
        private enum FlyState {
            Patrol,
            Attack,
            PreExplode,
            Dying
        }

        private sealed class GiantFlyStateMachine : SimpleStateMachine<FlyState> {
            public GiantFlyStateMachine(GiantFlyController ctrl) : base(FlyState.Patrol) {
                PermitIf(FlyState.Patrol, FlyState.Attack, () => ctrl.ShouldEnterAttackFromPatrol());

                PermitFromMany(FlyState.PreExplode, FlyState.Patrol, FlyState.Attack);

                PermitAfter(FlyState.PreExplode, FlyState.Dying, Mathf.Max(0f, ctrl.preExplodeDuration));
            }
        }

        [Header("Movement")]
        [SerializeField, Tooltip("Base horizontal movement speed used in patrol and as attack baseline.")]
        private float linearSpeed = 3f;

        [SerializeField, Tooltip("Distance of forward raycast used to detect obstacles.")]
        private float obstacleCheckDistance = 0.3f;

        [SerializeField, Tooltip("Layers treated as blocking walls for patrol, attack and line-of-sight checks.")]
        private LayerMask wallLayer = (1 << 10) | (1 << 11);

        [Header("Lifetime")]
        [SerializeField, Tooltip("Minimum random lifetime before self-destruction flow starts.")]
        private float minLifetime = 13f;

        [SerializeField, Tooltip("Maximum random lifetime before self-destruction flow starts.")]
        private float maxLifetime = 15f;

        [Header("Patrol Phase")]
        [SerializeField, Tooltip("Frequency of vertical wobble while patrolling.")]
        private float patrolFluctuationFrequency = 3.1f;

        [SerializeField, Tooltip("Amplitude of vertical wobble while patrolling.")]
        private float patrolFluctuationAmplitude = 0.25f;

        [SerializeField, Tooltip("How quickly velocity follows patrol target direction.")]
        private float patrolResponsiveness = 8f;

        [Header("Attack Phase")]
        [SerializeField, Tooltip("Initial speed when entering attack state.")]
        private float attackStartSpeed = 3.5f;

        [SerializeField, Tooltip("Maximum speed reachable during attack acceleration.")]
        private float attackMaxSpeed = 7f;

        [SerializeField, Tooltip("Speed increase per second during attack.")]
        private float attackAcceleration = 7f;

        [SerializeField, Tooltip("How quickly attack velocity aligns to desired direction.")]
        private float attackSteering = 9f;

        [SerializeField, Tooltip("How long obstacle blocking is tolerated in attack before pre-explode.")]
        private float attackBlockedDestroyDelay = 1f;

        [SerializeField, Tooltip("Maximum time spent in attack before pre-explode.")]
        private float attackTimeout = 4f;

        [SerializeField, Tooltip("Distance to remembered attack point required to trigger pre-explode.")]
        private float attackReachDistance = 0.2f;

        [Header("Player Detection")]
        [SerializeField, Tooltip("Layers considered as player targets for sight checks.")]
        private LayerMask sightLayer = 1 << 9;

        [SerializeField, Tooltip("Sight radius used to detect player and start attack.")]
        private float sightRadius = 3f;

        [SerializeField, Tooltip("Layers considered as player targets for damage-trigger proximity.")]
        private LayerMask damageTriggerLayer = 1 << 9;

        [SerializeField, Tooltip("Radius used to detect close player contact and start pre-explode.")]
        private float damageTriggerRadius = 0.4f;

        [Header("Pre-Explosion")]
        [SerializeField, Tooltip("Time spent in pre-explode state before switching to destroy animation.")]
        private float preExplodeDuration = 1f;

        [FormerlySerializedAs("preExplodeFluctuationAmplitude")]
        [FormerlySerializedAs("preExplodeShakeAmplitude")]
        [SerializeField, Tooltip("Maximum shake amplitude reached by the end of pre-explode.")]
        private float preExplodeMaxAmplitude = 0.12f;

        [FormerlySerializedAs("preExplodeFluctuationFrequency")]
        [SerializeField, Tooltip("Shake update frequency (position hops per second) in pre-explode.")]
        private float preExplodeShakeFrequency = 5f;

        [Header("Destroy")]
        [SerializeField, Tooltip("Fallback auto-destroy delay after entering destroy animation.")]
        private float destroyFallbackDelay = 1f;

        [Header("Sounds")]
        [SerializeField, Tooltip("Looped movement sound played while fly is alive.")]
        private AudioCue flyingSound;

        [SerializeField, Tooltip("One-shot sound played when fly starts bloating before explosion.")]
        private AudioCue bloatSound;
        
        [SerializeField, Tooltip("One-shot sound played when destroy animation starts.")]
        private AudioCue destroySound;

        private readonly Collider2D[] sightResults = new Collider2D[8];
        private readonly Collider2D[] damageTriggerResults = new Collider2D[8];
        private readonly RaycastHit2D[] wallHits = new RaycastHit2D[4];

        private MultiStateSpriteAnimator anim;
        private Rigidbody2D rb;
        private Collider2D mainCollider;
        private CircleCollider2D damageTriggerCollider;
        private IAudioLoopHandle flyingSoundHandle;
        private GiantFlyStateMachine stateMachine;

        private float actualLifetime;
        private float lifetime;
        private float attackSpeed;
        private float blockedTimer;
        private float attackTimer;
        private float fluctuationSeed;
        private Vector2 attackTargetPoint;
        private Vector2 pendingAttackTargetPoint;
        private bool hasPendingAttackTarget;
        private Vector2 preExplodeAnchor;
        private float preExplodeNextShakeTime;
        private Vector2 pendingPreExplodeAnchor;
        private bool hasPendingPreExplodeAnchor;
        private bool destroyScheduled;
        private IAudioLoopHandle moveSoundHandle;
        private GameObject damageArea;
        private Facing2D facing;

        private void Awake() {
            anim = GetComponent<MultiStateSpriteAnimator>();
            rb = GetComponent<Rigidbody2D>();
            mainCollider = GetComponent<Collider2D>();
            damageTriggerCollider = transform.Find("DamageTrigger")?.GetComponent<CircleCollider2D>();
            damageArea = transform.Find("DamageArea")?.gameObject;
            facing = GetComponent<Facing2D>();

            fluctuationSeed = Random.Range(0f, 1000f);
            stateMachine = new GiantFlyStateMachine(this);
            stateMachine.OnTransition += OnStateTransition;
            CloseDamageWindow();
        }

        private void CloseDamageWindow() {
            damageArea.SetActive(false);
        }
        
        // Invoked from animator
        public void OpenDamageWindow() {
            damageArea.SetActive(true);
        }

        public void OnAnimationFrame(MultiStateSpriteAnimator anim) {
            Debug.Log($">>>> {anim.CurrentClip.Name}");
            if (anim.CurrentClip.Name == "destroy" && anim.CurrentFrameIndex == 4) {
                OpenDamageWindow();
            }
        }

        private void OnEnable() {
            ResetRuntimeState();
        }

        private void OnDisable() {
            if (flyingSoundHandle != null) {
                flyingSoundHandle.Stop();
                flyingSoundHandle = null;
            }
        }

        private void Start() {
            EnsureMoveAnimation();
            EnsureFlyingSound();
        }

        private void FixedUpdate() {
            if (stateMachine == null) {
                return;
            }

            var dt = Time.fixedDeltaTime;

            if (stateMachine.State != FlyState.Dying) {
                lifetime += dt;
                if (lifetime >= actualLifetime) {
                    RequestPreExplode(rb.position);
                }
            }

            switch (stateMachine.State) {
                case FlyState.Patrol:
                    UpdatePatrol(dt);
                    break;

                case FlyState.Attack:
                    UpdateAttack(dt);
                    break;

                case FlyState.PreExplode:
                    UpdatePreExplode();
                    break;

                case FlyState.Dying:
                    rb.velocity = Vector2.zero;
                    break;
            }

            stateMachine.Update(dt);
        }

        public void SetFacingDir(FacingDir dir) {
            facing.SetDir(dir);
        }

        public void OnHit(HitInfo hit) {
            StartDestroy();
        }

        public void OnDeath() {
            Destroy(gameObject);
        }

        private void UpdatePatrol(float dt) {
            if (IsObstacleAhead(facing.DirVector, obstacleCheckDistance)) {
                facing.FlipDir();
            }

            float sway = Mathf.Sin((Time.time + fluctuationSeed) * patrolFluctuationFrequency) * patrolFluctuationAmplitude;
            var moveDir = new Vector2(facing.DirSign, sway).normalized;
            var targetVelocity = moveDir * linearSpeed;
            rb.velocity = Vector2.Lerp(rb.velocity, targetVelocity, patrolResponsiveness * dt);
        }

        private void UpdateAttack(float dt) {
            attackTimer += dt;
            if (attackTimer >= attackTimeout) {
                RequestPreExplode(rb.position);
                return;
            }

            if (IsPlayerInDamageTrigger()) {
                RequestPreExplode(rb.position);
                return;
            }

            Vector2 toTarget = attackTargetPoint - rb.position;
            float reachDistance = Mathf.Max(0.01f, attackReachDistance);
            if (toTarget.sqrMagnitude <= reachDistance * reachDistance) {
                RequestPreExplode(attackTargetPoint);
                return;
            }

            attackSpeed = Mathf.Min(attackSpeed + attackAcceleration * dt, attackMaxSpeed);

            Vector2 desiredDir = toTarget.sqrMagnitude > 0.0001f
                ? toTarget.normalized
                : facing.DirVector;

            if (!Mathf.Approximately(desiredDir.x, 0f)) {
                facing.SetByX(desiredDir.x);
            }

            Vector2 desiredVelocity = desiredDir * attackSpeed;
            rb.velocity = Vector2.Lerp(rb.velocity, desiredVelocity, attackSteering * dt);

            var checkDir = rb.velocity.sqrMagnitude > 0.0001f ? rb.velocity.normalized : desiredDir;
            if (IsObstacleAhead(checkDir, obstacleCheckDistance)) {
                blockedTimer += dt;
                if (blockedTimer >= attackBlockedDestroyDelay) {
                    RequestPreExplode(rb.position);
                }
            } else {
                blockedTimer = 0f;
            }
        }

        private void UpdatePreExplode() {
            float progress = preExplodeDuration <= 0f
                ? 1f
                : Mathf.Clamp01(stateMachine.Progress);

            float amplitude = preExplodeMaxAmplitude * Mathf.Lerp(0.35f, 1f, progress * progress);

            float shakeFrequency = Mathf.Max(1f, preExplodeShakeFrequency);
            float shakeInterval = 1f / shakeFrequency;
            if (stateMachine.TimeInState >= preExplodeNextShakeTime) {
                var dir = Random.insideUnitCircle;
                if (dir.sqrMagnitude < 0.0001f) {
                    dir = Vector2.right;
                }

                var targetPosition = preExplodeAnchor + dir.normalized * amplitude;
                rb.position = targetPosition;
                preExplodeNextShakeTime = stateMachine.TimeInState + shakeInterval;
            }

            rb.velocity = Vector2.zero;
        }

        private bool ShouldEnterAttackFromPatrol() {
            if (!IsPlayerInSight(out var playerPosition)) {
                return false;
            }

            pendingAttackTargetPoint = playerPosition;
            hasPendingAttackTarget = true;
            return true;
        }

        private void RequestPreExplode(Vector2 anchorPoint) {
            if (stateMachine == null) {
                return;
            }

            if (stateMachine.State == FlyState.PreExplode || stateMachine.State == FlyState.Dying) {
                return;
            }

            pendingPreExplodeAnchor = anchorPoint;
            hasPendingPreExplodeAnchor = true;
            stateMachine.Go(FlyState.PreExplode);
        }

        private void OnStateTransition(FlyState toState, FlyState _) {
            switch (toState) {
                case FlyState.Patrol:
                    rb.velocity = Vector2.zero;
                    break;

                case FlyState.Attack:
                    EnterAttackState();
                    break;

                case FlyState.PreExplode:
                    EnterPreExplodeState();
                    break;

                case FlyState.Dying:
                    EnterDyingState();
                    break;
            }
        }

        private void EnterAttackState() {
            attackTargetPoint = hasPendingAttackTarget
                ? pendingAttackTargetPoint
                : rb.position + facing.DirVector;
            hasPendingAttackTarget = false;

            attackTimer = 0f;
            blockedTimer = 0f;
            attackSpeed = Mathf.Max(attackStartSpeed, linearSpeed);

            float deltaX = attackTargetPoint.x - rb.position.x;
            if (!Mathf.Approximately(deltaX, 0f)) {
                facing.SetByX(deltaX);
            }
        }

        private void EnterPreExplodeState() {
            anim.SetClip("bloat");
            
            if (flyingSoundHandle != null) {
                flyingSoundHandle.Stop();
                flyingSoundHandle = null;
            }

            if (bloatSound != null) {
                G.Audio.PlayAt(bloatSound, transform.position);
            }
            
            preExplodeAnchor = hasPendingPreExplodeAnchor ? pendingPreExplodeAnchor : rb.position;
            hasPendingPreExplodeAnchor = false;
            preExplodeNextShakeTime = 0f;
            
            rb.velocity *= 0.25f;
        }

        private void EnterDyingState() {
            rb.velocity = Vector2.zero;
            anim.SetClip("destroy");

            if (mainCollider != null) {
                mainCollider.enabled = false;
            }

            if (destroySound != null) {
                G.Audio.PlayAt(destroySound, transform.position);
            }

            if (!destroyScheduled) {
                destroyScheduled = true;
                Destroy(gameObject, destroyFallbackDelay);
            }
        }

        private bool IsPlayerInSight(out Vector2 playerPosition) {
            playerPosition = rb.position;

            if (sightLayer.value == 0 || sightRadius <= 0f) {
                return false;
            }

            int count = Physics2D.OverlapCircleNonAlloc(rb.position, sightRadius, sightResults, sightLayer);
            if (count <= 0) {
                return false;
            }

            Collider2D closest = null;
            float closestSqr = float.PositiveInfinity;

            for (int i = 0; i < count; i++) {
                var candidate = sightResults[i];
                if (candidate == null) {
                    continue;
                }

                Vector2 candidatePos = candidate.bounds.center;
                float deltaX = candidatePos.x - rb.position.x;
                if (deltaX * facing.DirSign < 0f) {
                    continue;
                }

                float sqr = (candidatePos - rb.position).sqrMagnitude;
                if (sqr < closestSqr) {
                    closestSqr = sqr;
                    closest = candidate;
                    playerPosition = candidatePos;
                }
            }

            if (closest == null) {
                return false;
            }

            Vector2 dirToPlayer = playerPosition - rb.position;
            if (wallLayer.value != 0 && dirToPlayer.sqrMagnitude > 0.0001f) {
                int hitCount = Physics2D.RaycastNonAlloc(
                    rb.position,
                    dirToPlayer.normalized,
                    wallHits,
                    dirToPlayer.magnitude,
                    wallLayer
                );

                if (hitCount > 0) {
                    return false;
                }
            }

            return true;
        }

        private bool IsPlayerInDamageTrigger() {
            if (damageTriggerLayer.value == 0) {
                return false;
            }

            Vector2 center = damageTriggerCollider != null ? damageTriggerCollider.bounds.center : rb.position;
            float radius = damageTriggerRadius;

            if (damageTriggerCollider != null) {
                float colliderRadius = damageTriggerCollider.radius * Mathf.Abs(damageTriggerCollider.transform.lossyScale.x);
                radius = Mathf.Max(radius, colliderRadius);
            }

            int count = Physics2D.OverlapCircleNonAlloc(center, radius, damageTriggerResults, damageTriggerLayer);
            return count > 0;
        }

        private bool IsObstacleAhead(Vector2 dir, float distance) {
            if (wallLayer.value == 0 || distance <= 0f || dir.sqrMagnitude < 0.0001f) {
                return false;
            }

            int count = Physics2D.RaycastNonAlloc(rb.position, dir.normalized, wallHits, distance, wallLayer);
            return count > 0;
        }

        private void EnsureMoveAnimation() {
            if (anim != null) {
                anim.SetClip("move");
            }
        }

        private void EnsureFlyingSound() {
            if (flyingSound != null && flyingSoundHandle == null) {
                flyingSoundHandle = G.Audio.PlayLoopFollow(flyingSound, transform, is3D: true);
            }
        }

        private void ResetRuntimeState() {
            lifetime = 0f;
            actualLifetime = Random.Range(minLifetime, maxLifetime);
            attackTimer = 0f;
            blockedTimer = 0f;
            attackSpeed = Mathf.Max(attackStartSpeed, linearSpeed);
            hasPendingAttackTarget = false;
            hasPendingPreExplodeAnchor = false;
            destroyScheduled = false;

            facing.RefreshDir();

            if (mainCollider != null) {
                mainCollider.enabled = true;
            }

            preExplodeAnchor = rb != null ? rb.position : (Vector2)transform.position;
            rb.velocity = Vector2.zero;

            stateMachine?.ResetTo(FlyState.Patrol);
            EnsureMoveAnimation();
            EnsureFlyingSound();
        }

        private void StartDestroy() {
            RequestPreExplode(rb != null ? rb.position : transform.position);
        }

        private void OnDrawGizmosSelected() {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, sightRadius);

            Gizmos.color = Color.yellow;
            Vector3 damageCenter = damageTriggerCollider != null ? damageTriggerCollider.bounds.center : transform.position;
            Gizmos.DrawWireSphere(damageCenter, damageTriggerRadius);

            Gizmos.color = Color.red;
            Vector3 obstacleDir = new Vector3(facing.DirSign, 0f, 0f);
            Gizmos.DrawLine(transform.position, transform.position + obstacleDir * obstacleCheckDistance);

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(preExplodeAnchor, preExplodeMaxAmplitude);
        }
    }
}
