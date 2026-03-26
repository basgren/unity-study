using Core;
using Game.Core.Audio;
using Game.Core.Services.Bootstrap;
using Prefabs.Characters.Common;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

namespace Game.Features.Characters._Shared {
    /// <summary>
    /// Kinematic projectile with deterministic movement and ricochet.
    ///
    /// Why this component does custom sweep-based movement instead of plain MovePosition:
    /// - With kinematic bodies, moving the full step and then reacting to collisions can miss thin/nearby geometry.
    /// - After a downward ricochet near ground, the next frame may begin partially inside tilemap colliders.
    /// - That overlap can produce "bouncing inside geometry" artifacts.
    ///
    /// To prevent this while staying kinematic, movement is performed as:
    /// 1) Sweep cast along intended movement vector.
    /// 2) Move only up to hit distance minus a skin.
    /// 3) Ricochet and separate slightly away from the hit normal.
    /// 4) Continue with remaining distance in the same FixedUpdate (bounded loop).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class LinearProjectile : ProjectileBase {
        [Header("Ricochet")]
        [SerializeField]
        private int maxRicochetCount;

        [SerializeField]
        [Range(0f, 180f)]
        private float firstRicochetMinAngle = 25f;

        [SerializeField]
        [Range(0f, 180f)]
        private float firstRicochetMaxAngle = 70f;

        [Header("Collision")]
        [SerializeField]
        private LayerMask ricochetLayers = 1 << 10;

        [SerializeField]
        [Min(1f)]
        private int maxSweepsPerFixedStep = 3;

        [Header("Effects")]
        [SerializeField]
        private GameObject destroyEffectPrefab;
        
        [SerializeField]
        private UnityEvent onDestroy;

        // Pixel-art project: keep collision tolerances snapped to pixel units.
        // Half pixel is enough to avoid overlap artifacts and visually imperceptible in motion.
        private const float CastSkin = CoreConst.HalfPixelSize;
        private const float PostRicochetSeparation = CoreConst.HalfPixelSize;
        
        private readonly RaycastHit2D[] castHits = new RaycastHit2D[8];

        private int ricochetCount;
        private bool isBroken;

        private PlaySfxOnCall sfx;
        private Collider2D myCollider;
        private ContactFilter2D castFilter;
        
        protected override void Awake() {
            base.Awake();

            sfx = GetComponent<PlaySfxOnCall>();
            myCollider = GetComponent<Collider2D>();

            // Helps collision reliability for fast kinematic bodies.
            // We still do manual sweeps, but this improves contact generation consistency.
            MyRigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            MyRigidbody.useFullKinematicContacts = true;

            castFilter = default;
            castFilter.useTriggers = false;
            castFilter.useLayerMask = true;

            // Explicit obstacle mask for ricochet sweeps.
            // If this is too broad, projectile can hit unrelated nearby colliders at spawn and appear "stuck".
            castFilter.layerMask = ricochetLayers.value == 0
                ? Physics2D.GetLayerCollisionMask(gameObject.layer)
                : ricochetLayers;
        }

        /// <summary>
        /// Performs swept movement for this frame.
        ///
        /// Important details:
        /// - Uses collider cast to detect first obstacle before entering it.
        /// - Uses CastSkin to stop slightly before the surface (avoids resting exactly on boundaries).
        /// - Supports multiple reflections in one fixed step (maxSweepsPerFixedStep).
        /// - Applies PostRicochetSeparation to avoid starting next iteration/frame in overlap.
        /// </summary>
        protected override Vector2 GetNewPosition(Vector2 currentPos) {
            if (isBroken) {
                return currentPos;
            }

            Vector2 position = currentPos;
            Vector2 remainingMovement = Direction * (linearSpeed * DeltaTime);
            float minMoveSqr = CastSkin * CastSkin;

            for (int i = 0; i < maxSweepsPerFixedStep && remainingMovement.sqrMagnitude > minMoveSqr; i++) {
                // Re-cast from latest intermediate position. Without this, second sweep in the same frame
                // would query from stale frame-start position and produce incorrect hits.
                MyRigidbody.position = position;

                Vector2 movementDirection = Direction;
                float remainingDistance = remainingMovement.magnitude;

                int hitCount = myCollider.Cast(movementDirection, castFilter, castHits, remainingDistance + CastSkin);
                if (!TryGetNearestHit(hitCount, movementDirection, out RaycastHit2D hit)) {
                    // Nothing blocks this step: consume all remaining motion.
                    position += remainingMovement;
                    break;
                }

                // Stop slightly before impact point to maintain separation from surface.
                float moveDistance = Mathf.Max(0f, hit.distance - CastSkin);
                position += movementDirection * moveDistance;

                if (ricochetCount < maxRicochetCount) {
                    DoRicochet(hit.normal);

                    // We step slightly away from the hit surface before continuing movement.
                    // Without this, the next frame can start overlapped and bounce inside tilemaps.
                    position += hit.normal * PostRicochetSeparation;

                    // Continue within the same frame using reflected direction.
                    float leftoverDistance = Mathf.Max(0f, remainingDistance - moveDistance);
                    remainingMovement = Direction * leftoverDistance;
                    continue;
                }

                BreakSelf();
                return position;
            }

            return position;
        }

        private void BreakSelf() {
            if (isBroken) {
                return;
            }

            isBroken = true;
            if (destroyEffectPrefab != null) {
                G.Spawner.SpawnVfx(destroyEffectPrefab, transform.position);
            }

            onDestroy?.Invoke();
            
            sfx?.Play("destroy");
            Destroy(gameObject);
        }

        public void OnHitPlayer() {
            Destroy(gameObject);
        }

        /// <summary>
        /// Ricochet rules:
        /// - First ricochet: randomized angle from inverted direction.
        /// - Next ricochets: physically reflected by hit normal.
        /// </summary>
        private void DoRicochet(Vector2 hitNormal) {
            Vector2 currentDirection = Direction;
            if (ricochetCount == 0) {
                Direction = GetRandomFirstRicochetDirection(currentDirection);
            } else if (hitNormal.sqrMagnitude > Mathf.Epsilon) {
                Direction = Vector2.Reflect(currentDirection, hitNormal.normalized).normalized;
            } else {
                Direction = -currentDirection;
            }

            sfx?.Play("ricochet");
            ricochetCount++;
        }

        /// <summary>
        /// Picks nearest valid hit from cast buffer.
        ///
        /// The normal-facing check rejects contacts whose normals are not opposing incoming movement.
        /// That filters out invalid/overlap artifacts that can happen around spawn or complex geometry edges.
        /// </summary>
        private bool TryGetNearestHit(int hitCount, Vector2 movementDirection, out RaycastHit2D hit) {
            hit = default;
            if (hitCount <= 0) {
                return false;
            }

            bool found = false;
            float minDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++) {
                RaycastHit2D candidate = castHits[i];
                if (candidate.collider == null) {
                    continue;
                }

                // Only consider surfaces that face the incoming movement. This prevents selecting
                // invalid contacts produced by initial overlaps or side/back-facing query artifacts.
                if (Vector2.Dot(movementDirection, candidate.normal) >= -0.0001f) {
                    continue;
                }
                
                if (candidate.distance < minDistance) {
                    minDistance = candidate.distance;
                    hit = candidate;
                    found = true;
                }
            }

            return found;
        }

        private Vector2 GetRandomFirstRicochetDirection(Vector2 currentDirection) {
            float minAngle = Mathf.Min(firstRicochetMinAngle, firstRicochetMaxAngle);
            float maxAngle = Mathf.Max(firstRicochetMinAngle, firstRicochetMaxAngle);
            float angle = Random.Range(minAngle, maxAngle);
            float signedAngle = Random.value > 0.5f ? angle : -angle;

            return (Quaternion.Euler(0f, 0f, signedAngle) * -currentDirection).normalized;
        }
    }
}
