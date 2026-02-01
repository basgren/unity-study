using System;
using Core.Utils;
using UnityEngine;

namespace Game.Controllers {
    public enum SpriteOrientation {
        Left,
        Right,
    }

    public class BaseCharacterAnimKeys {
        public static readonly int IsGrounded = Animator.StringToHash("isGrounded");
        public static readonly int IsRunning = Animator.StringToHash("isRunning");
        public static readonly int VelocityY = Animator.StringToHash("velocityY");
    }
    
    [RequireComponent(typeof(Rigidbody2D))]
    public class BaseCharacterController : MonoBehaviour {
        /// <summary>
        /// Layers, which collisions should be checked to detect if player stands on ground.
        /// </summary>
        [SerializeField]
        private LayerMask groundLayer;
        
        [SerializeField]
        private float moveSpeed = 5f; // Run speed

        /// <summary>
        /// Parameter that specified where imported sprite faces - left or right. 
        /// </summary>
        [SerializeField]
        private SpriteOrientation baseSpriteOrientation = SpriteOrientation.Right;

        public Vector2 Direction { get; private set; }
        public bool IsGrounded { get; private set; }
        
        protected Rigidbody2D MyRigidbody;
        protected BoxCollider2D MyCollider;
        protected Animator MyAnimator;

        /// <summary>
        /// Returns the height the player fell from. This should be checked only when
        /// <c>IsGrounded</c> or <c>IsLandedThisFrame</c> is true.
        /// </summary>
        protected float FallHeight => fallUpperPosY - fallLowerPosY;
        protected MultiRayCaster GroundChecker;
        protected MultiRayCaster CeilingChecker;
        
        private float fallUpperPosY;
        private float fallLowerPosY;
        
        protected virtual void Awake() {
            MyRigidbody = GetComponent<Rigidbody2D>();
            MyAnimator = GetComponent<Animator>();
            MyCollider = GetComponent<BoxCollider2D>();

            GroundChecker = MultiRayCaster.CreateGroundChecker(MyCollider, groundLayer)
                // Remove adjustment to prevent double jump when jumping along the wall
                .WithAdjustment(0f); 
            
            CeilingChecker = new MultiRayCaster(MyCollider, groundLayer)
                .WithDirection(Direction2D.Up)
                .WithIgnoreOneWayPlatforms();
            
            ResetFallHeight();
        }
        
        protected virtual void Update() {
            // We won't use InputSystem events, as order of their invocation is not guaranteed, but
            // in case we want to check button combinations, it's easier to check them manually.
            // Using events is better for UI controls.
            // CheckGround();
        }

        private void FixedUpdate() {
            CheckGround();
        }

        protected virtual void LateUpdate() {
            // Update animator at the end, when player state is updated.
            if (MyAnimator != null) {
                UpdateAnimator();                
            }
        }
        
        // ---=== Public interface ===---
        public void SetDirection(Vector2 dir, bool preserveSpriteOrientation = false) {
            Direction = dir;
            var vx = Math.Sign(dir.x) * GetMoveSpeed();
            MyRigidbody.velocity = new Vector2(vx, MyRigidbody.velocity.y);

            if (!preserveSpriteOrientation) {
                float dirScale = baseSpriteOrientation == SpriteOrientation.Right ? 1 : -1;
                
                if (dir.x > 0) {
                    transform.localScale = new Vector3(1 * dirScale, transform.localScale.y, transform.localScale.z);
                } else if (dir.x < 0) {
                    transform.localScale = new Vector3(-1 * dirScale, transform.localScale.y, transform.localScale.z);
                }                
            }
        }
        
        public void TeleportTo(Vector3 targetPosition) {
            transform.position = targetPosition;
            // Reset fall height to prevent fall dust to appear when player is teleported to a lower position.
            ResetFallHeight();
        }
        
        // ---=== Protected Methods ===---
        protected virtual void UpdateAnimator() {
            MyAnimator.SetBool(BaseCharacterAnimKeys.IsGrounded, IsGrounded);
            MyAnimator.SetBool(BaseCharacterAnimKeys.IsRunning, IsRunning());
            var velocityY = MyRigidbody.velocity.y;

            // Adjustments to compensate for floating point precision errors and physics jitter.
            if (Math.Abs(velocityY) < 0.001f) {
                velocityY = 0;
            }

            MyAnimator.SetFloat(BaseCharacterAnimKeys.VelocityY, velocityY);
        }

        protected virtual float GetMoveSpeed() {
            return moveSpeed;
        }
        
        protected bool IsRunning() {
            return Math.Abs(MyRigidbody.velocity.x) > 0.01f;
        }
        
        // ---=== Private Methods ===---
        protected virtual void CheckGround() {
            GroundChecker.Update();
            CeilingChecker.Update();
            
            UpdateFallHeight();
            
            IsGrounded = GroundChecker.HasCollision;
        }
        
        private void UpdateFallHeight() {
            float y = MyCollider.bounds.min.y;

            if (GroundChecker.HasExitedCollisionThisFrame) {
                fallUpperPosY = y;
                fallLowerPosY = y;
            } else if (GroundChecker.HasEnteredCollisionThisFrame) {
                fallLowerPosY = y;
            } else if (!IsGrounded) {
                fallUpperPosY = Mathf.Max(fallUpperPosY, y);
            }
        }
        
        private void ResetFallHeight() {
            fallUpperPosY = transform.position.y;
            fallLowerPosY = transform.position.y;
        }
        
        protected virtual void OnDrawGizmosSelected() {
            GroundChecker?.DrawGizmos();
            CeilingChecker?.DrawGizmos();
        }
    }
}
