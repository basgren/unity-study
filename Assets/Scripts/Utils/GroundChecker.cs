using System;
using UnityEngine;

namespace Utils {
    public class GroundChecker {
        public bool IsGrounded { get; private set; }
        public bool WasGroundedLastFrame { get; private set; }
        public float RayLength { get; }
        public Vector2 RayDirection { get; }
        
        /// <summary>
        /// Stores rays origins after the most recent updates. Useful for drawing gizmos and debugging.
        /// </summary>
        public Vector2[] RayOrigins { get; }

        public readonly int rayCount;
        
        private RaycastHit2D[] rayHits;

        private readonly BoxCollider2D myCollider;
        private LayerMask groundLayer;

        public GroundChecker(BoxCollider2D myCollider, LayerMask groundLayer, int rayCount = 3) {
            if (rayCount < 2) {
                throw new ArgumentException("raysCount must be at least 2");
            }
            
            this.myCollider = myCollider;
            this.groundLayer = groundLayer;
            this.rayCount = rayCount;

            rayHits = new RaycastHit2D[rayCount];
            RayOrigins = new Vector2[rayCount];
            RayLength = AllConst.UnitsPerPixel * 1.2f;
            RayDirection = Vector2.down;
        }

        /// <summary>
        /// Should be call when ground flags should be updated. Call this method in the beginning
        /// of every frame before checking input.
        /// </summary>
        public void Update() {
            WasGroundedLastFrame = IsGrounded;
            
            Bounds bounds = myCollider.bounds;
            float delta = bounds.size.x / (rayCount - 1);

            bool hasHit = false;
            
            for (var i = 0; i < rayCount; i++) {
                Vector2 origin = new Vector2(
                    bounds.min.x + delta * i,
                    bounds.min.y
                );
                
                RaycastHit2D hit = Physics2D.Raycast(origin, RayDirection, RayLength, groundLayer);

                if (hit.collider != null) {
                    hasHit = true;                    
                }

                RayOrigins[i] = origin;  
                rayHits[i] = hit;
            }
            
            IsGrounded = hasHit;
        }

        public bool HasRayCollision(int i) {
            return rayHits[i].collider != null;
        }
    }
}
