using System;
using Core.Services;
using PixelCrew.Controllers;
using UnityEngine;

namespace Prefabs.Characters.Sharky {
    public class SharkyController : BaseCharacterController {
        public static class SharkyAnimKeys {
            public static readonly int IsGrounded = Animator.StringToHash("isGrounded");
            public static readonly int IsRunning = Animator.StringToHash("isRunning");
            public static readonly int IsDead = Animator.StringToHash("isDead");
            public static readonly int VelocityY = Animator.StringToHash("velocityY");
            public static readonly int OnJump = Animator.StringToHash("onJump");
            public static readonly int OnHit = Animator.StringToHash("onHit");
            public static readonly int OnDeath = Animator.StringToHash("onDeath");
            public static readonly int OnAttack = Animator.StringToHash("onAttack");
        }
        
        private SharkyStateMachine stateMachine;

        protected override void Awake() {
            base.Awake();

            stateMachine = G.StateMachines.Create<SharkyStateMachine>(this);
            stateMachine.GoLater(2f, SharkyState.Patrol);
        }

        protected override void Update() {
            base.Update();
            
            Debug.Log($"State: {stateMachine.State}");
        }

        protected override void UpdateAnimator() {
            animator.SetBool(SharkyAnimKeys.IsRunning, IsRunning());
            var velocityY = rb.velocity.y;
            
            // Adjustments to compensate for floating point precision errors and physics jitter.
            if (Math.Abs(velocityY) < 0.001f) {
                velocityY = 0;
            }

            animator.SetFloat(SharkyAnimKeys.VelocityY, velocityY);
        }
        
        private bool IsRunning() {
            return Math.Abs(rb.velocity.x) > 0.01f;
        }
    }
}
