using System;
using UnityEngine;

namespace Utils {
    /// <summary>
    /// <p>A lightweight raycast-based ground detection utility designed for 2D platformer object.</p>
    /// <p>The GroundChecker performs a configurable number of downward raycasts from the bottom
    /// of the character’s BoxCollider2D to determine whether the character is standing on solid ground.
    /// If at least one raycast detects collision with the ground, it's considered that player is grounded.</p>  
    /// <br/>
    /// <p><b>IMPORTANT:</b></p>
    /// <p>For accurate grounding and landing timing:
    /// <b>Disable Rigidbody2D interpolation ("Interpolation = None") on the character's Rigidbody2D.</b></p>
    /// 
    /// Unity's interpolation moves the visual Transform between physics updates,
    /// which introduces one-frame positional offsets. This causes incorrect landing positions,
    /// preliminary ground detection, and misaligned landing effects.
    /// 
    /// GroundChecker must operate on stable, non-interpolated physics positions to work reliably.
    /// A better option will be to move from Unity's physics to a custom physics engine, thad does
    /// all checks synchronously in `Update()` method.
    /// </summary>
    public class GroundChecker {
        /// <summary>
        /// Whether the character is currently standing on the ground (based on downward raycasts).
        /// </summary>
        public bool IsGrounded { get; private set; }

        /// <summary>
        /// Grounded state from the previous frame. Used to detect transitions.
        /// </summary>
        public bool WasGroundedLastFrame { get; private set; }

        /// <summary>
        /// Returns true when player became grounded in this frame.
        /// </summary>
        public bool IsLandedThisFrame => IsGrounded && !WasGroundedLastFrame;

        /// <summary>
        /// Returns true when player became ungrounded this frame (jumped, fell off the ground, etc).
        /// </summary>
        public bool IsLeftGroundThisFrame => !IsGrounded && WasGroundedLastFrame;

        /// <summary>
        /// Stores rays origins after the most recent updates. Useful for drawing gizmos and debugging.
        /// </summary>
        public Vector2[] RayOrigins { get; }

        public float RayLength { get; }
        public Vector2 RayDirection { get; }
        public bool IsAllGrounded { get; private set; }

        public readonly int RayCount;

        private readonly RaycastHit2D[] rayHits;
        private readonly RaycastHit2D[] hitsBuffer;
        private readonly BoxCollider2D myCollider;
        private ContactFilter2D contactFilter;

        public GroundChecker(BoxCollider2D myCollider, LayerMask groundLayer, int rayCount = 3) {
            if (rayCount < 2) {
                throw new ArgumentException("raysCount must be at least 2");
            }

            this.myCollider = myCollider;
            RayCount = rayCount;

            rayHits = new RaycastHit2D[rayCount];
            RayOrigins = new Vector2[rayCount];

            // Buffer for all hits
            hitsBuffer = new RaycastHit2D[8];

            RayLength = AllConst.PixelSize * 0.8f; // a bit less than pixel to avoid preliminary collisions
            RayDirection = Vector2.down;

            // Настраиваем фильтр один раз
            contactFilter = new ContactFilter2D {
                useLayerMask = true,
                layerMask = groundLayer,
                useTriggers = false
            };
        }

        /// <summary>
        /// Should be called when ground flags should be updated. 
        /// Call this method at the beginning of every frame before checking input.
        /// </summary>
        public void Update() {
            WasGroundedLastFrame = IsGrounded;

            Bounds bounds = myCollider.bounds;
            float delta = bounds.size.x / (RayCount - 1);

            bool hasHit = false;
            IsAllGrounded = true;

            for (int i = 0; i < RayCount; i++) {
                Vector2 origin = new Vector2(
                    bounds.min.x + delta * i,
                    bounds.min.y
                );

                RayOrigins[i] = origin;

                // Take all hits, not only the first one 
                int hitCount = Physics2D.Raycast(origin, RayDirection, contactFilter, hitsBuffer, RayLength);

                RaycastHit2D chosenHit = default;
                bool foundValidHit = false;

                for (int j = 0; j < hitCount; j++) {
                    RaycastHit2D h = hitsBuffer[j];

                    if (h.collider == null || h.collider == myCollider) {
                        continue;
                    }

                    chosenHit = h;
                    foundValidHit = true;
                    break;
                }

                rayHits[i] = chosenHit;

                if (foundValidHit) {
                    hasHit = true;
                } else {
                    IsAllGrounded = false;
                }
            }

            IsGrounded = hasHit;
        }

        public bool HasRayCollision(int i) {
            return rayHits[i].collider != null;
        }
    }
}
