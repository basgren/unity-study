using System;
using UnityEngine;

namespace PixelCrew.Controllers {
    public enum SpriteOrientation {
        Left,
        Right,
    }
    
    [RequireComponent(typeof(Rigidbody2D))]
    public class BaseCharacterController : MonoBehaviour {
        [SerializeField]
        private float moveSpeed = 5f; // Run speed

        /// <summary>
        /// Parameter that specified where imported sprite faces - left or right. 
        /// </summary>
        [SerializeField]
        private SpriteOrientation baseSpriteOrientation = SpriteOrientation.Right;

        public Vector2 Direction { get; private set; }

        protected Rigidbody2D MyRigidbody;
        protected Animator MyAnimator;

        protected virtual void Awake() {
            MyRigidbody = GetComponent<Rigidbody2D>();
            MyAnimator = GetComponent<Animator>();
        }
        
        protected virtual void Update() {
            // do nothing for now.
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
            var vx = Math.Sign(dir.x) * moveSpeed;
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
        
        // ---=== Protected Methods ===---
        protected virtual void UpdateAnimator() {
            // Override in descendants to update animator in the  LateUpdate method.
        }
    }
}
