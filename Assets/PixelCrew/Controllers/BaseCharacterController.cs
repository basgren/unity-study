using System;
using UnityEngine;

namespace PixelCrew.Controllers {

    [RequireComponent(typeof(Rigidbody2D))]
    public class BaseCharacterController : MonoBehaviour {
        [SerializeField]
        private float moveSpeed = 5f; // Run speed

        public Vector2 Direction { get; private set; }

        protected Rigidbody2D rb;
        protected Animator animator;

        protected virtual void Awake() {
            rb = GetComponent<Rigidbody2D>();
            animator = GetComponent<Animator>();
        }
        
        protected virtual void Update() {
            // do nothing for now.
        }

        protected virtual void LateUpdate() {
            // Update animator at the end, when player state is updated.
            if (animator != null) {
                UpdateAnimator();                
            }
        }
        
        // ---=== Public interface ===---
        public void SetDirection(Vector2 dir) {
            Direction = dir;
            var vx = Math.Sign(dir.x) * moveSpeed;
            rb.velocity = new Vector2(vx, rb.velocity.y);
        }
        
        // ---=== Protected Methods ===---
        protected virtual void UpdateAnimator() {
            // Override in descendants to update animator in the  LateUpdate method.
        }
    }
}
