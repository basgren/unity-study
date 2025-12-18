using System;
using System.Collections.Generic;
using System.Linq;
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
    public class MultiRayCaster {
        public static MultiRayCaster CreateGroundChecker(BoxCollider2D myCollider, LayerMask groundLayer) {
            return new MultiRayCaster(myCollider, groundLayer)
                .WithDirection(Direction2D.Down)
                .WithAdjustment(AllConst.PixelSize);
        }
        
        private const int MaxCollisionsPerRay = 8;

        public bool HasCollision { get; private set; }
        public bool HadCollisionLastFrame { get; private set; }
        public bool HasEnteredCollisionThisFrame => HasCollision && !HadCollisionLastFrame;
        public bool HasExitedCollisionThisFrame => !HasCollision && HadCollisionLastFrame;

        /// <summary>
        /// Stores rays origins after the most recent updates. Useful for drawing gizmos and debugging.
        /// </summary>
        public Vector2[] RayOrigins { get; private set; }

        public Direction2D RayDirection { get; private set; }
        public float RayLength { get; private set; }
        public int RayCount { get; private set; }
        public bool IsAllCollide { get; private set; }
        public float Adjustment { get; private set; }

        private readonly BoxCollider2D myCollider;
        private RaycastHit2D[] rayHits;
        private RaycastHit2D[][] hitsBuffer;
        private int[] hitCounts;
        private ContactFilter2D contactFilter;
        private Vector2 rayDirVector;
        private Collider2D[] excludedColliders;

        public MultiRayCaster(BoxCollider2D myCollider, LayerMask groundLayer) {
            this.myCollider = myCollider;
            contactFilter = new ContactFilter2D {
                useLayerMask = true,
                layerMask = groundLayer,
                useTriggers = false
            };

            // Defaults
            WithRayCount(3)
                .WithDirection(Direction2D.Down)
                .WithRayLength(AllConst.PixelSize * 0.8f); // a bit less than pixel to avoid preliminary collisions
        }

        /// <summary>
        /// Should be called when ground flags should be updated. 
        /// Call this method at the beginning of every frame before checking input.
        /// </summary>
        public void Update() {
            HadCollisionLastFrame = HasCollision;
            Vector2 rayGap = GetRayGap();

            bool hasHit = false;
            IsAllCollide = true;

            for (int i = 0; i < RayCount; i++) {
                Vector2 origin = GetRayOrigin(i, rayGap);
                RayOrigins[i] = origin;

                // Take all hits, not only the first one 
                int hitCount = Physics2D.Raycast(origin, rayDirVector, contactFilter, hitsBuffer[i], RayLength);
                hitCounts[i] = hitCount;

                RaycastHit2D chosenHit = default;
                bool foundValidHit = false;

                for (int j = 0; j < hitCount; j++) {
                    RaycastHit2D h = hitsBuffer[i][j];

                    if (CanCollideWith(h.collider)) {
                        chosenHit = h;
                        foundValidHit = true;
                        break;
                    }
                }

                rayHits[i] = chosenHit;

                if (foundValidHit) {
                    hasHit = true;
                } else {
                    IsAllCollide = false;
                }
            }

            HasCollision = hasHit;
        }

        public List<T> GetHitComponents<T>() where T : Component {
            var result = new HashSet<T>();

            for (int rayIndex = 0; rayIndex < RayCount; rayIndex++) {
                int count = hitCounts[rayIndex];

                for (int i = 0; i < count; i++) {
                    RaycastHit2D hit = hitsBuffer[rayIndex][i];

                    if (!CanCollideWith(hit.collider)) {
                        continue;
                    }

                    if (hit.collider.TryGetComponent<T>(out var component)) {
                        result.Add(component);
                    }
                }
            }

            return result.ToList();
        }

        public void DrawGizmos() {
            for (var i = 0; i < RayCount; i++) {
                Gizmos.color = HasRayCollision(i) ? Color.red : Color.green;

                Vector2 rayOrigin = RayOrigins[i];
                Gizmos.DrawLine(rayOrigin, rayOrigin + RayLength * rayDirVector);
            }
        }

        private bool CanCollideWith(Collider2D collider) {
            if (collider == null || collider == myCollider) {
                return false;
            }

            if (excludedColliders != null && Array.IndexOf(excludedColliders, collider) != -1) {
                return false;
            }

            return true;
        }

        private Vector2 GetRayOrigin(int rayIndex, Vector2 rayGap) {
            var bounds = myCollider.bounds;
            Vector2 start;
            float adjustment = Adjustment * 0.5f;

            switch (RayDirection) {
                case Direction2D.Down:
                    start = new Vector2(bounds.min.x - adjustment, bounds.min.y);
                    break;

                case Direction2D.Up:
                    start = new Vector2(bounds.min.x - adjustment, bounds.max.y);
                    break;

                case Direction2D.Left:
                    start = new Vector2(bounds.min.x, bounds.min.y - adjustment);
                    break;

                default:
                    start = new Vector2(bounds.max.x, bounds.min.y - adjustment);
                    break;
            }

            return start + rayIndex * rayGap;
        }

        private Vector2 GetRayGap() {
            switch (RayDirection) {
                case Direction2D.Down:
                case Direction2D.Up:
                    float width = myCollider.bounds.size.x + Adjustment;
                    return Vector2.right * (width / (RayCount - 1));

                default:
                    float height = myCollider.bounds.size.y + Adjustment;
                    return Vector2.up * (height / (RayCount - 1));
            }
        }

        public bool HasRayCollision(int i) {
            return rayHits[i].collider != null;
        }

        #region Options

        public MultiRayCaster WithRayCount(int rayCount) {
            if (rayCount < 2) {
                throw new ArgumentException("raysCount must be at least 2");
            }

            RayCount = rayCount;
            RayOrigins = new Vector2[rayCount];
            rayHits = new RaycastHit2D[rayCount];
            hitsBuffer = new RaycastHit2D[rayCount][];
            hitCounts = new int[rayCount];

            for (int i = 0; i < rayCount; i++) {
                hitsBuffer[i] = new RaycastHit2D[MaxCollisionsPerRay];
            }

            return this;
        }

        public MultiRayCaster WithDirection(Direction2D direction) {
            RayDirection = direction;
            rayDirVector = Geometry.GetDirVector(direction);
            return this;
        }

        public MultiRayCaster WithRayLength(float rayLength) {
            RayLength = rayLength;
            return this;
        }
        
        /// <summary>
        /// Adjusts rays base by a given amount. For example, if set to 0.1, rays will be cast
        /// from a zone, wider by 0.1 block (0.05 for each side).
        /// </summary>
        /// <param name="adjustment"></param>
        /// <returns></returns>
        public MultiRayCaster WithAdjustment(float adjustment) {
            // TODO: Sometimes physics incorrectly counts `isGrounded` - the object already left ground,
            //   raycast shows that object is not grounded, but object hangs in the air like it was grounded.
            //   This is usually when object is in the air, but very close to ground edge. We can't do raycasts
            //   outside collider bounds to compensate it, as in this case jumping close to the wall will make
            //   raycast to detect collision with wall, setting isGrounded to true.
            //   Not sure how to solve it. Probably, ignore built-in physics and use kinematic Rigidbody2D instead
            //   with manual movement check.
            Adjustment = adjustment;
            return this;
        }

        public MultiRayCaster ExcludeColliders(params Collider2D[] collider) {
            excludedColliders = collider;
            return this;
        }

        #endregion
    }
}
