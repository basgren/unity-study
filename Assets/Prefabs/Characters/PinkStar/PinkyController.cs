using System;
using Core.Audio;
using Core.Components.Damage;
using Core.FSM;
using Core.Services;
using Game.Controllers;
using UnityEngine;

namespace Prefabs.Characters.PinkStar {
    internal abstract class PinkyAnimKeys : BaseCharacterAnimKeys {
        public static readonly int IsDead = Animator.StringToHash("isDead");
        public static readonly int IsAttacking = Animator.StringToHash("isAttacking");
        public static readonly int OnJump = Animator.StringToHash("onJump");
        public static readonly int OnHit = Animator.StringToHash("onHit");
        public static readonly int OnDeath = Animator.StringToHash("onDeath");
        public static readonly int OnAnticipation = Animator.StringToHash("onAnticipation");
    }
    
    public class PinkyController : BaseCharacterController, IPinkySensors {
        private const string DustPositionObjectName = "DustSpawnPoint";

        [SerializeField]
        private PinkyControlSource controlSource;

        [SerializeField]
        private float chaseSpeed = 6f;

        [SerializeField]
        private GameObject damageAreaObject;

        [Header("Effects")]
        [SerializeField]
        private GameObject runDustPrefab;

        [SerializeField]
        private AudioCue attackSound;

        private float attackCooldownTime = 2f;

        private PinkyStateMachine fsm;
        private PinkyAI ai;
        private float attackCooldownTimer;
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
        
        protected override void Awake() {
            base.Awake();
            
            ai = GetComponent<PinkyAI>();
            damageable = GetComponent<Damageable>();
            
            dustSpawnPoint = transform.Find(DustPositionObjectName);

            state = new PinkyStateMachine(PinkyState.Calm, this);
            state.OnStateEnter += OnPinkyStateEnter;
            state.OnStateExit += OnPinkyStateExit;
            G.StateMachines.Register(state, this);
            
            // damageAreaObject.SetActive(false);
        }

        protected override void Update() {
            base.Update();

            ExecuteCommands();
            
            if (knockbackStunTimer > 0f) {
                knockbackStunTimer -= Time.deltaTime;                
            }

            if (attackCooldownTimer > 0) {
                attackCooldownTimer -= Time.deltaTime;
            }

            if (hasKnockback && !IsStunned && IsGrounded) {
                hasKnockback = false;
                StopMovement();
            }
        }

        private void ExecuteCommands() {
            if (controlSource == null) {
                return;
            }

            PinkyCommand? command = controlSource.GetCommand();

            if (command == null) {
                return;
            }

            var value = command.Value;

            if (value.XDirection != 0 && !isAttacking) {
                SetDirection(value.XDirection > 0 ? Vector2.right : Vector2.left);
            } else {
                if (IsGrounded) {
                    SetDirection(Vector2.zero);
                }
            }

            if (value.Attack && state.CanGo(PinkyState.Anticipating)) {
                isAttackStarted = true;
                // StartAttack();
            }
        }
        
        private void OnPinkyStateExit(PinkyState curState, PinkyState nextState) {
            Debug.Log($"Exiting state: {curState} -> {nextState}");
        }

        private void OnPinkyStateEnter(PinkyState curState, PinkyState prevState) {
            Debug.Log($"Entering state: {curState} <- {prevState}");
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

        protected override float GetMoveSpeed() {
            return ai?.BehaviorState == PinkyBehaviorState.Attacking
                ? chaseSpeed
                : base.GetMoveSpeed();
        }

        public void StartAttack() {
            StopMovement();
            MyAnimator.SetTrigger(PinkyAnimKeys.OnAnticipation);
        }

        public void OnAttackStartedFrame() {
            MyAnimator.SetBool(PinkyAnimKeys.IsAttacking, true);
        }

        public void EndAttackFrame() {
            MyAnimator.SetBool(PinkyAnimKeys.IsAttacking, false);
        }

        public void Attack() {
            StopMovement();
            // MyAnimator.SetTrigger(PinkyAnimKeys.OnAttack);
        }

        public void OpenDamageWindow() {
            isAttacking = true;
            damageAreaObject.SetActive(true);

            Vector2 dir = Vector2.right * transform.lossyScale.x;
            MyRigidbody.velocity = dir * 1f + Vector2.up * 1f;

            if (attackSound != null) {
                G.Audio.PlayAt(attackSound, transform.position);                
            }
        }

        public void CloseDamageWindow() {
            damageAreaObject.SetActive(false);
        }

        public void FinishAttack() {
            isAttacking = false;
            attackCooldownTimer = attackCooldownTime;
        }

        public void StopMovement() {
            SetDirection(Vector2.zero);
        }

        public bool CanAttack() {
            return !isDead && !isAttacking && attackCooldownTimer <= 0;
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
        
        public void OnAfterHit(Damager damager) {
            knockbackStunTimer = knockbackStunTime;
            hasKnockback = true;
            wasHit = true;

            Debug.Log($"Sharky: Hit by {damager.Type}. Health: {damageable.Health}");

            if (damageable.IsDead) {
                Debug.Log(">>>> sharky is dead");
                isDiedThisFrame = true;
                isDead = true;
                ai.enabled = false;
            }
        }

        public bool IsAttackTriggered() {
            Debug.Log($">>> check {isAttackStarted}");
            var result = isAttackStarted;
            isAttackStarted = false;
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
