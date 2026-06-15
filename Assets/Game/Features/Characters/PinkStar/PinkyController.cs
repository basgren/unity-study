using System;
using Core.Audio;
using Game.Core.Bootstrap;
using Game.Core.Components.Collisions;
using Game.Core.Components.Damage;
using Game.Core.Services.SceneState.Savers;
using Game.Features.Characters._Shared;
using Prefabs.Characters.PinkStar;
using UnityEngine;

namespace Game.Features.Characters.PinkStar {
    internal abstract class PinkyAnimKeys : BaseCharacterAnimKeys {
        public static readonly int IsDead = Animator.StringToHash("isDead");
        public static readonly int IsAttacking = Animator.StringToHash("isAttacking");
        public static readonly int IsCooldown = Animator.StringToHash("isCooldown");
        public static readonly int OnJump = Animator.StringToHash("onJump");
        public static readonly int OnHit = Animator.StringToHash("onHit");
        public static readonly int OnDeath = Animator.StringToHash("onDeath");
        public static readonly int OnAnticipation = Animator.StringToHash("onAnticipation");
    }
    
    /// <summary>
    /// NOTES:
    /// Circle collider should not touch ground, otherwise it will always count as collision with ground and won't
    /// enter collisions with walls.
    /// </summary>
    public class PinkyController : BaseCharacterController, IPinkySensors {
        private const string DustPositionObjectName = "DustSpawnPoint";

        [SerializeField]
        private PinkyControlSource controlSource;

        [SerializeField]
        private float patrolSpeed = 1.7f;

        [SerializeField]
        private float chaseSpeed = 3f;

        [SerializeField]
        private float attackMaxSpeed = 15f;

        [SerializeField]
        private AnimationCurve attackSpeedCurve;

        [SerializeField]
        private GameObject damageAreaObject;

        [SerializeField]
        private Collider2D bodyColliderWhenAttacking;

        [SerializeField]
        private GameObject touchDamager;

        [Header("Hit Reaction")]
        [SerializeField]
        [Tooltip("Stun time after being hit before Pinky can turn/move again (delay before charging the player).")]
        private float knockbackStunTime = 0.6f;

        [SerializeField]
        [Tooltip("Upward impulse applied when a player hit lands during the roll attack (dodge leap over the player).")]
        private float dodgeHopImpulse = 10f;

        [Header("Effects")]
        [SerializeField]
        private GameObject runDustPrefab;

        [SerializeField]
        private AudioCue attackSound;
        
        [SerializeField]
        private AudioCue deadGroundedSound;

        [SerializeField]
        [Tooltip("Plays when a player hit lands on Pinky during the roll attack (the dodge leap instead of taking damage).")]
        private AudioCue rollHitSound;

        private GameObject visionObject;
        private LayerCheck vision;
        private bool isAttacking;
        private Transform dustSpawnPoint;
        private Damageable damageable;

        private bool hasKnockback;
        private bool wasHit;

        private float knockbackStunTimer;
        private bool isDiedThisFrame;
        private bool isDead;
        private bool IsStunned => knockbackStunTimer > 0f;
        public bool IsDead => isDead;
        public int LastHitSourceDirection { get; private set; }

        private PinkyStateMachine state;
        private PinkyAI ai;

        private bool isAttackStarted;
        private int lastRequestedLookDirection = 1;
        private int currentAttackDirection = 1;
        private readonly float verticalHopImpulse = 10f;
        private bool isHit = false;

        private int spottedPlayerDirection;

        private IAudioLoopHandle attackSoundHandle;

        protected override void Awake() {
            base.Awake();

            ai = GetComponent<PinkyAI>();
            damageable = GetComponent<Damageable>();
            damageable.DamageBlocked += OnDamageBlocked;

            dustSpawnPoint = transform.Find(DustPositionObjectName);
            if (vision == null) {
                visionObject = transform.Find("Vision").gameObject;

                if (visionObject != null) {
                    vision = visionObject.GetComponent<LayerCheck>();
                }
            }

            state = new PinkyStateMachine(PinkyState.Calm, this);
            state.OnTransition += OnPinkyTransition;
            G.StateMachines.Register(state, this);

            CloseDamageWindow();
        }

        private void OnDestroy() {
            if (damageable != null) {
                damageable.DamageBlocked -= OnDamageBlocked;
            }
        }

        protected override void Update() {
            base.Update();

            ExecuteCommands();

            if (knockbackStunTimer > 0f) {
                knockbackStunTimer -= Time.deltaTime;
            }

            if (hasKnockback && !IsStunned && IsGrounded) {
                hasKnockback = false;
                StopMovement();
            }
        }

        protected override void FixedUpdate() {
            base.FixedUpdate();

            if (state.State == PinkyState.Attacking) {
                var attackDir = Mathf.Sign(currentAttackDirection);
                if (attackDir == 0f) {
                    attackDir = 1f;
                }

                SetDirection(attackDir > 0f ? Vector2.right : Vector2.left);

                if (attackSoundHandle != null) {
                    var minPitch = 0.7f;
                    float pitch = minPitch + MyRigidbody.velocity.magnitude / attackMaxSpeed * (1 - minPitch);
                    attackSoundHandle.SetPitch(pitch);
                }
            }
        }
        
        private void OnCollisionEnter2D(Collision2D collision) {
            if (!isAttacking) {
                return;
            }

            if (!TryGetWallNormal(collision, out Vector2 wallNormal)) {
                return;
            }
            
            var bounceDir = new Vector2(wallNormal.x, 1f).normalized;
            currentAttackDirection = bounceDir.x > 0f ? 1 : -1;
            SetDirection(bounceDir);
            
            MyRigidbody.AddForce(Vector2.up * verticalHopImpulse, ForceMode2D.Impulse);
        }

        private bool TryGetWallNormal(Collision2D collision, out Vector2 wallNormal) {
            wallNormal = default;

            for (var i = 0; i < collision.contactCount; i++) {
                var n = collision.GetContact(i).normal;

                // Check that collision point is a wall, not the ceiling/floor
                if (Mathf.Abs(n.x) > 0.6f && Mathf.Abs(n.y) < 0.6f) {
                    wallNormal = n;
                    return true;
                }
            }

            return false;
        }

        protected override float GetMoveSpeed() {
            if (state.State == PinkyState.Attacking) {
                return attackSpeedCurve.Evaluate(state.Progress) * attackMaxSpeed;
            }

            if (ai != null) {
                return ai.Behavior == PinkyBehavior.Hunting || ai.Behavior == PinkyBehavior.Recovering
                    ? chaseSpeed
                    : patrolSpeed;
            }

            return base.GetMoveSpeed();
        }

        private void ExecuteCommands() {
            if (controlSource == null) {
                return;
            }

            if (isDead || IsStunned) {
                return;
            }

            PinkyCommand? command = controlSource.GetCommand();

            if (command == null
                || state.State == PinkyState.Cooldown
                || state.State == PinkyState.Attacking
                || state.State == PinkyState.Anticipating
               ) {
                return;
            }

            var value = command.Value;

            if (state.State != PinkyState.Attacking) {
                if (value.XDirection != 0 && !isAttacking) {
                    lastRequestedLookDirection = value.XDirection > 0 ? 1 : -1;
                    var lookDirection = value.XDirection > 0 ? Vector2.right : Vector2.left;

                    if (ai != null && ai.Behavior == PinkyBehavior.Recovering) {
                        // In Recovering we only want to rotate sprite, not move.
                        SetDirection(lookDirection);
                        SetDirection(Vector2.zero, true);
                    } else {
                        SetDirection(lookDirection);
                    }
                } else {
                    if (IsGrounded) {
                        SetDirection(Vector2.zero);
                    }
                }
            }

            if (value.Attack && state.CanGo(PinkyState.Anticipating)) {
                currentAttackDirection = value.XDirection != 0
                    ? (value.XDirection > 0 ? 1 : -1)
                    : lastRequestedLookDirection;
                isAttackStarted = true;
            }
        }

        private void OnPinkyTransition(PinkyState toState, PinkyState fromState) {
            switch (toState) {
                case PinkyState.Anticipating:
                    Anticipate();
                    break;

                case PinkyState.Attacking:
                    OpenDamageWindow();
                    break;

                case PinkyState.Cooldown:
                    CloseDamageWindow();
                    // TODO: [BG] Cooldown animation - some deep breating or something like that 
                    MyAnimator.SetBool(PinkyAnimKeys.IsCooldown, true);
                    break;

                case PinkyState.Hit:
                    OnAfterHit();
                    break;
            }
            
            switch (fromState) {
                case PinkyState.Cooldown:
                    MyAnimator.SetBool(PinkyAnimKeys.IsCooldown, false);
                    break;
            }
        }

        protected override void UpdateAnimator() {
            base.UpdateAnimator();

            if (wasHit) {
                wasHit = false;
                MyAnimator.SetTrigger(PinkyAnimKeys.OnHit);
            }

            if (isDiedThisFrame) {
                MyAnimator.SetTrigger(PinkyAnimKeys.OnDeath);
                // TODO: [BG] Actually should be reset somewhere else, not in this method, but not it's just for POC 
                isDiedThisFrame = false;
            }

            MyAnimator.SetBool(PinkyAnimKeys.IsDead, isDead);
        }

        public void Anticipate() {
            StopMovement();
            MyAnimator.SetTrigger(PinkyAnimKeys.OnAnticipation);
        }

        private void OpenDamageWindow() {
            isAttacking = true;
            visionObject.SetActive(false);
            damageAreaObject.SetActive(true);
            damageable.IgnoreDamage = true;
            touchDamager.SetActive(false);
            bodyColliderWhenAttacking.enabled = true;
            MyAnimator.SetBool(PinkyAnimKeys.IsAttacking, true);

            Vector2 dir = spottedPlayerDirection > 0 ? Vector2.right : Vector2.left;
            spottedPlayerDirection = 0;
            SetDirection(dir);
            MyRigidbody.velocity = dir * 1f + Vector2.up * 1f;
            MyRigidbody.gravityScale = 0.7f;

            if (attackSound != null) {
                StopAttackSound();
                attackSoundHandle = G.Audio.PlayLoopFollow(attackSound, transform);
            }
        }

        private void StopAttackSound() {
            if (attackSoundHandle != null) {
                attackSoundHandle.Stop();
                attackSoundHandle = null;
            }
        }

        private void CloseDamageWindow() {
            StopAttackSound();
            touchDamager.SetActive(true);
            damageable.IgnoreDamage = false;
            damageAreaObject.SetActive(false);
            visionObject.SetActive(true);
            MyRigidbody.gravityScale = 1f;
            bodyColliderWhenAttacking.enabled = false;
            isAttacking = false;
            MyAnimator.SetBool(PinkyAnimKeys.IsAttacking, false);
        }

        private void StopMovement() {
            SetDirection(Vector2.zero);
        }

        public void SpawnRunDust() {
            if (Math.Abs(MyRigidbody.velocity.x) > 1f) {
                var instance = G.Spawner.SpawnVfx(runDustPrefab, dustSpawnPoint.position);
                Facing.ApplyTo(instance);
            }
        }

        public void OnHit() {
            // TODO: [BG] simplify this hit detection chain.
            wasHit = true;
        }

        /// <summary>
        /// Called when a player hit is registered but blocked by <c>IgnoreDamage</c>. During the roll
        /// attack this means a well-timed hit: Pinky leaps upward (dodge) over the player instead of
        /// taking damage, keeping its horizontal roll momentum.
        /// </summary>
        private void OnDamageBlocked(Damager damager) {
            if (isDead || state.State != PinkyState.Attacking) {
                return;
            }

            MyRigidbody.velocity = new Vector2(MyRigidbody.velocity.x, 0f);
            MyRigidbody.AddForce(Vector2.up * dodgeHopImpulse, ForceMode2D.Impulse);

            if (rollHitSound != null) {
                G.Audio.PlayAt(rollHitSound, transform.position);
            }
        }
        
        public void OnAfterHit() {
            knockbackStunTimer = knockbackStunTime;
            hasKnockback = true;
            LastHitSourceDirection = GetHitSourceDirectionFromKnockback();
            ai?.OnHitReceived(LastHitSourceDirection);

            // Debug.Log($"Sharky: Hit by {damager.Type}. Health: {damageable.Health}");

            if (damageable.IsDead) {
                isDiedThisFrame = true;
                isDead = true;
                touchDamager.SetActive(false);
                
                var destroyedSaver = GetComponent<DestructionStateSaver>();
                if (destroyedSaver != null) {
                    destroyedSaver.MarkDestroyed();
                }
            }
        }

        private int GetHitSourceDirectionFromKnockback() {
            var knockbackDirX = Mathf.Sign(MyRigidbody.velocity.x);
            if (knockbackDirX > 0f) {
                return -1;
            }

            if (knockbackDirX < 0f) {
                return 1;
            }

            return 0;
        }

        public bool IsAttackTriggered() {
            var result = isAttackStarted;
            isAttackStarted = false; // reset value after reading
            return result;
        }

        public bool IsPlayerInSight() {
            var isInSight = vision != null && vision.IsColliding();

            if (isInSight && spottedPlayerDirection != 0) {
                spottedPlayerDirection = GetFacingDirSign();
            }
            
            return isInSight;
        }

        bool IPinkySensors.IsGrounded() {
            return GroundChecker.HasCollision;
        }

        public bool IsHit() {
            return wasHit;
        }
        
        public void OnGroundedDead() {
            G.Audio.Play2D(deadGroundedSound);
        }
    }
}
