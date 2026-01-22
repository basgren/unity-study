using System;
using Core.Components.Damage;
using Core.Services;
using Game.Controllers;
using UnityEngine;

namespace Prefabs.Characters.Sharky {
    internal abstract class SharkyAnimKeys : BaseCharacterAnimKeys {
        public static readonly int IsDead = Animator.StringToHash("isDead");
        public static readonly int OnJump = Animator.StringToHash("onJump");
        public static readonly int OnHit = Animator.StringToHash("onHit");
        public static readonly int OnDeath = Animator.StringToHash("onDeath");
        public static readonly int OnAnticipation = Animator.StringToHash("onAnticipation");
        public static readonly int OnAttack = Animator.StringToHash("onAttack");
    }
    
    public class SharkyController : BaseCharacterController {
        private const string DustPositionObjectName = "DustSpawnPoint";

        [SerializeField]
        private float chaseSpeed = 3f;

        [SerializeField]
        private GameObject damageAreaObject;
        
        [Header("Effects")]
        [SerializeField]
        private GameObject runDustPrefab;

        private float attackCooldownTime = 2f;

        private SharkyStateMachine stateMachine;
        private SharkyAI ai;
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

        protected override void Awake() {
            Debug.Log("Sharky awake");
            base.Awake();

            ai = GetComponent<SharkyAI>();
            damageable = GetComponent<Damageable>();
            
            dustSpawnPoint = transform.Find(DustPositionObjectName);

            stateMachine = G.StateMachines.Create<SharkyStateMachine>(this);
            stateMachine.GoLater(2f, SharkyState.Patrol);

            damageAreaObject.SetActive(false);
        }

        protected override void Update() {
            base.Update();

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

        protected override void UpdateAnimator() {
            base.UpdateAnimator();
            
            if (wasHit) {
                wasHit = false;
                MyAnimator.SetTrigger(SharkyAnimKeys.OnHit);
            }
            
            if (isDiedThisFrame) {
                MyAnimator.SetTrigger(SharkyAnimKeys.OnDeath);
                // TODO: [BG] Actually should be reset somewhere else, not in this method, but not it's just for POC 
                isDiedThisFrame = false;
            }
            
            MyAnimator.SetBool(SharkyAnimKeys.IsDead, isDead);
        }

        protected override float GetMoveSpeed() {
            return ai?.BehaviorState == SharkyBehaviorState.Chasing
                ? chaseSpeed
                : base.GetMoveSpeed();
        }

        public void Anticipate() {
            StopMovement();
            MyAnimator.SetTrigger(SharkyAnimKeys.OnAnticipation);
        }

        public void Attack() {
            StopMovement();
            MyAnimator.SetTrigger(SharkyAnimKeys.OnAttack);
        }

        public void OpenDamageWindow() {
            isAttacking = true;
            damageAreaObject.SetActive(true);

            Vector2 dir = Vector2.right * transform.lossyScale.x;
            MyRigidbody.velocity = dir * 1f + Vector2.up * 1f;
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
    }
}
