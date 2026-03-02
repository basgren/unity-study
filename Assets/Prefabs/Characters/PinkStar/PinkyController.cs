using System;
using Core.Audio;
using Core.Components.Damage;
using Core.Services;
using Game.Controllers;
using UnityEngine;

namespace Prefabs.Characters.PinkStar {
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
        private float chaseSpeed = 6f;

        [SerializeField]
        private float attackMaxSpeed = 15f;

        [SerializeField]
        private AnimationCurve attackSpeedCurve;

        [SerializeField]
        private GameObject damageAreaObject;

        [SerializeField]
        private Collider2D bodyColliderWhenAttacking;

        [Header("Effects")]
        [SerializeField]
        private GameObject runDustPrefab;

        [SerializeField]
        private AudioCue attackSound;

        private bool isAttacking;
        private Transform dustSpawnPoint;
        private Damageable damageable;

        private bool hasKnockback;
        private bool wasHit;

        private float knockbackStunTime = 0.2f;
        private float knockbackStunTimer;
        private bool isDiedThisFrame;
        private bool isDead;
        private bool IsStunned => knockbackStunTimer > 0f;
        public bool IsDead => isDead;

        private PinkyStateMachine state;

        private bool isAttackStarted;
        private readonly float verticalHopImpulse = 10f;

        protected override void Awake() {
            base.Awake();

            damageable = GetComponent<Damageable>();

            dustSpawnPoint = transform.Find(DustPositionObjectName);

            state = new PinkyStateMachine(PinkyState.Calm, this);
            state.OnStateEnter += OnPinkyStateEnter;
            state.OnStateExit += OnPinkyStateExit;
            G.StateMachines.Register(state, this);

            CloseDamageWindow();
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
                var v = MyRigidbody.velocity;
                var sign = Mathf.Sign(transform.localScale.x);
                SetDirection(Vector2.left * sign);
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

            return base.GetMoveSpeed();
        }

        private void ExecuteCommands() {
            if (controlSource == null) {
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
                    SetDirection(value.XDirection > 0 ? Vector2.right : Vector2.left);
                } else {
                    if (IsGrounded) {
                        SetDirection(Vector2.zero);
                    }
                }
            }

            if (value.Attack && state.CanGo(PinkyState.Anticipating)) {
                isAttackStarted = true;
            }
        }

        private void OnPinkyStateEnter(PinkyState curState, PinkyState prevState) {
            Debug.Log($"Entering state: {curState} <- {prevState}");
            switch (curState) {
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
        }

        private void OnPinkyStateExit(PinkyState curState, PinkyState nextState) {
            switch (curState) {
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
            damageAreaObject.SetActive(true);
            bodyColliderWhenAttacking.enabled = true;
            MyAnimator.SetBool(PinkyAnimKeys.IsAttacking, true);

            Vector2 dir = Vector2.right * transform.lossyScale.x;
            MyRigidbody.velocity = dir * 1f + Vector2.up * 1f;
            MyRigidbody.gravityScale = 0.7f;

            if (attackSound != null) {
                G.Audio.PlayAt(attackSound, transform.position);
            }
        }

        private void CloseDamageWindow() {
            damageAreaObject.SetActive(false);
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

                // Make sure the spawned object is directed in the same direction as target object.
                // But for sharky negate x axis, as its original sprite asset looks left, while player
                // sprite looks right.
                var ls = dustSpawnPoint.lossyScale;
                instance.transform.localScale = new Vector3(-ls.x, ls.y, ls.z);
            }
        }

        public void OnAfterHit() {
            knockbackStunTimer = knockbackStunTime;
            hasKnockback = true;
            wasHit = true;

            // Debug.Log($"Sharky: Hit by {damager.Type}. Health: {damageable.Health}");

            if (damageable.IsDead) {
                Debug.Log(">>>> sharky is dead");
                isDiedThisFrame = true;
                isDead = true;
            }
        }

        public bool IsAttackTriggered() {
            var result = isAttackStarted;
            isAttackStarted = false; // reset value after reading
            return result;
        }

        bool IPinkySensors.IsGrounded() {
            return GroundChecker.HasCollision;
        }

        public bool IsHit() {
            return false;
        }
    }
}
