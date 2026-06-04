using Core;
using Core.Utils;
using Game.Features.Dynamic;
using UnityEngine;

namespace Game.Features.Props.Barrel {
    /// <summary>
    /// Drives a barrel's motion as a kinematic Rigidbody2D. Vertical motion (manual gravity
    /// and landing) is owned here; horizontal motion only happens while dragging and is driven
    /// by <see cref="Game.Features.Hero.Abilities.DragAbility"/> through
    /// <see cref="DragStep"/>. Because the same raycast logic decides "grounded" and performs
    /// the move, a barrel can no longer hang on a ledge corner the way the physics solver let it.
    /// </summary>
    /// <remarks>
    /// All movement goes through a single <c>Rigidbody2D.MovePosition</c> call per FixedUpdate.
    /// MovePosition only remembers the last target set in a step, so vertical and horizontal
    /// deltas must be combined into one call — hence the drag coordinator hands its (already
    /// clamped) horizontal delta to <see cref="DragStep"/> instead of moving the body itself.
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class BarrelMotor : MonoBehaviour {
        [Tooltip("Maximum fall speed, in units per second (1 unit = 1 block).")]
        [SerializeField]
        private float maxFallSpeed = 12f;

        [Tooltip("Seconds for a falling barrel to accelerate from rest to its max fall speed. 0 = instant.")]
        [SerializeField]
        private float timeToMaxFallSpeed = 0.25f;

        [Tooltip("Layers the barrel treats as solid — ground and other barrels — for grounding and " +
                 "obstacle clamping. Should include the Ground and DynamicObjects layers.")]
        [SerializeField]
        private LayerMask obstacleMask;

        /// <summary>True when at least part of the barrel base rests on ground or another barrel.</summary>
        public bool IsGrounded { get; private set; }

        /// <summary>Signed horizontal distance actually moved during the most recent step (for SFX).</summary>
        public float LastHorizontalMove { get; private set; }

        /// <summary>
        /// Layers treated as solid by the barrel. Falls back to Ground + DynamicObjects by name if
        /// the field was left unset, so a barrel still works without Inspector configuration.
        /// Resolved lazily so it is valid regardless of Awake order between barrel components.
        /// </summary>
        public LayerMask ObstacleMask {
            get {
                if (!maskResolved) {
                    resolvedMask = obstacleMask.value != 0
                        ? obstacleMask
                        : LayerMask.GetMask("Ground", "DynamicObjects");
                    maskResolved = true;
                }

                return resolvedMask;
            }
        }

        private Rigidbody2D body;
        private BoxCollider2D box;
        private DraggableBarrel barrel;
        private LayerMask resolvedMask;
        private bool maskResolved;

        // Sub-pixel gap left when stopping at an obstacle so a body never rests at exactly zero
        // distance. Resting flush makes Rigidbody2D.Cast report that obstacle at distance 0 in
        // every direction, which would freeze the body (it could not move away). Shared with the
        // player clamp so both members of the convoy behave the same. Kept well under a pixel so
        // the gap renders flush under the pixel-perfect camera.
        public const float SkinWidth = CoreConst.PixelSize * 0.25f;

        // Downward probe range used for grounding and for settling onto the surface. Matches the
        // ground rays' length so "grounded" and "settle target" agree.
        private const float GroundProbe = CoreConst.PixelSize;

        private float verticalVelocity;
        private bool isDragged;
        private bool wasGroundedLastStep;

        // Raycast-based downward ground check (the same kind the player uses). Raycasts reliably
        // detect a surface a barrel rests flush against — a shape-cast is ambiguous at zero
        // distance, which previously made stacked / pushed barrels flicker their grounded state.
        private MultiRayCaster groundRays;

        // Reused cast buffer to avoid per-frame allocations (landing snap + horizontal clamp).
        private readonly RaycastHit2D[] castBuffer = new RaycastHit2D[8];

        private void Awake() {
            body = GetComponent<Rigidbody2D>();
            barrel = GetComponent<DraggableBarrel>();

            // This motor drives the body manually via MovePosition and cast-based clamping; a
            // dynamic body would fight it (double gravity, solver pushback). Enforce kinematic as a
            // safety net so the feature is correct even if a prefab wasn't migrated in the Inspector.
            body.bodyType = RigidbodyType2D.Kinematic;

            box = GetComponent<BoxCollider2D>();
            groundRays = MultiRayCaster
                .CreateGroundChecker(box, ObstacleMask)
                .WithRayCount(3)
                .WithRayLength(GroundProbe);

            groundRays.Update();
            IsGrounded = groundRays.HasCollision;
            wasGroundedLastStep = IsGrounded;
        }

        private void FixedUpdate() {
            // While dragged, DragAbility owns the move (it combines our gravity with the
            // clamped horizontal delta). Self-drive only when free.
            if (!isDragged) {
                ApplyStep(0f);
            }
        }

        /// <summary>
        /// Toggle drag ownership. When dragged, the drag coordinator must call <see cref="DragStep"/>
        /// each FixedUpdate; when released, the motor resumes self-driven gravity.
        /// </summary>
        public void SetDragged(bool dragged) {
            isDragged = dragged;
        }

        /// <summary>
        /// Returns the horizontal distance the barrel may move along <paramref name="dx"/> before
        /// hitting an obstacle, ignoring the passed convoy colliders. Does not move the body.
        /// </summary>
        public float ClampHorizontal(float dx, Collider2D[] exclude) {
            if (Mathf.Approximately(dx, 0f)) {
                return 0f;
            }

            return ClampMoveWithSkin(body, dx, ObstacleMask, exclude, castBuffer);
        }

        /// <summary>
        /// Performs one dragged step: applies gravity (if airborne) and the caller-clamped
        /// horizontal delta in a single MovePosition. <paramref name="horizontalDelta"/> is expected
        /// to be pre-clamped by the coordinator (so player and barrel move by the same amount).
        /// </summary>
        public void DragStep(float horizontalDelta) {
            ApplyStep(horizontalDelta);
        }

        private void ApplyStep(float horizontalDelta) {
            float dt = Time.fixedDeltaTime;

            groundRays.Update();
            bool grounded = groundRays.HasCollision;
            float vy = 0f;

            if (grounded) {
                verticalVelocity = 0f;
                vy = ComputeSettleDelta();
            } else {
                if (timeToMaxFallSpeed <= 0f) {
                    verticalVelocity = -maxFallSpeed;
                } else {
                    float accel = maxFallSpeed / timeToMaxFallSpeed;
                    verticalVelocity = Mathf.Max(verticalVelocity - accel * dt, -maxFallSpeed);
                }

                float fallDistance = -verticalVelocity * dt;
                // Cast a touch beyond the step so we catch the surface and rest a skin-width short
                // of it, instead of ending exactly flush (where detection is ambiguous next step).
                float castDistance = fallDistance + SkinWidth;
                // Ignore colliders this body already overlaps: the ground rays just said nothing
                // is below, so a distance-0 hit can only be an overlap artifact — e.g. a
                // scene-authored stack where the top barrel slightly overlaps this one. Without
                // this, a stacked pair pushed off a ledge "lands" mid-air on the barrel ABOVE it
                // (overlaps are reported at distance 0 in every cast direction) and hangs forever.
                float allowedDown = ClampDistance(
                    body, Vector2.down, castDistance, ObstacleMask, null, castBuffer, ignoreOverlapped: true);

                if (allowedDown < castDistance) {
                    // Landing: rest a sub-pixel gap above the surface and stop falling.
                    vy = -Mathf.Max(0f, allowedDown - SkinWidth);
                    grounded = true;
                    verticalVelocity = 0f;
                } else {
                    vy = -fallDistance;
                }
            }

            Vector2 delta = new Vector2(horizontalDelta, vy);
            if (delta != Vector2.zero) {
                body.MovePosition(body.position + delta);
            }

            LastHorizontalMove = horizontalDelta;

            if (grounded && !wasGroundedLastStep) {
                barrel?.OnLanded();
            }

            IsGrounded = grounded;
            wasGroundedLastStep = grounded;
        }

        /// <summary>
        /// Computes the vertical settle delta for a grounded barrel, converging it to the ONE
        /// shared rest height: where the downward cast reports exactly <see cref="SkinWidth"/> of
        /// clearance — the same equilibrium a landing produces. Uniformity matters: barrels at
        /// identical reported clearance can slide across onto each other (equal tops), and the
        /// snap-down kills the residual up-to-a-ray-length gap that shows as a stray pixel after
        /// a fall. A hit at distance 0 means the support is
        /// flush, overlapped, or inside the cast's contact-offset dead zone (all report 0); the
        /// barrel must never snap down through such a contact. If that contact is a box-shaped
        /// support below us — scene-authored stacks sit ~0.3px into the barrel underneath (0.75
        /// grid spacing vs ≈0.759 collider stack height) — the barrel instead lifts in skin-sized
        /// steps until the cast reports positive clearance again. The whole clamping model assumes
        /// overlap never exists: an overlapped collider is reported by Cast at distance 0 in every
        /// direction, which blocks horizontal dragging and fakes mid-air landings.
        /// </summary>
        private float ComputeSettleDelta() {
            ContactFilter2D filter = new ContactFilter2D {
                useLayerMask = true,
                layerMask = ObstacleMask,
                useTriggers = false
            };

            float castDistance = GroundProbe + SkinWidth;
            int count = body.Cast(Vector2.down, filter, castBuffer, castDistance);

            float toSurface = castDistance;
            bool blockedAtZero = false;
            bool liftableSupport = false;
            float myCenterY = box.bounds.center.y;

            for (int i = 0; i < count; i++) {
                RaycastHit2D hit = castBuffer[i];
                if (hit.collider == null) {
                    continue;
                }

                if (hit.distance > 0f) {
                    if (hit.distance < toSurface) {
                        toSurface = hit.distance;
                    }
                    continue;
                }

                blockedAtZero = true;

                // Lift only out of plain box supports below us (stacked barrels, box tiles).
                // Composite/tilemap contacts and side-by-side overlaps stay on the hold path:
                // their geometry can't be fixed by moving up.
                if (hit.collider is BoxCollider2D && hit.collider.bounds.center.y < myCenterY) {
                    liftableSupport = true;
                }
            }

            if (blockedAtZero) {
                // One skin-step up per frame, self-terminating: as soon as the cast reports a
                // positive distance the branch below takes over and finishes the convergence.
                return liftableSupport ? SkinWidth : 0f;
            }

            // Two-sided convergence to reported clearance == SkinWidth: snap down when above,
            // micro-lift (< SkinWidth) when below. Every grounded barrel resting at the SAME
            // reported clearance is what lets a barrel slide across onto an equal-height
            // neighbor: a barrel settled even a hair lower clips the neighbor's top corner and
            // its horizontal cast reads the neighbor as a wall.
            return -(toSurface - SkinWidth);
        }

        /// <summary>
        /// Returns the signed distance <paramref name="rb"/> may move along <paramref name="dx"/>,
        /// stopping a <see cref="SkinWidth"/> short of the first non-excluded obstacle so the body
        /// never ends flush against it. Shared by the dragged barrel and the player so both members
        /// of the convoy clamp identically.
        /// </summary>
        public static float ClampMoveWithSkin(
            Rigidbody2D rb,
            float dx,
            LayerMask mask,
            Collider2D[] exclude,
            RaycastHit2D[] buffer
        ) {
            if (Mathf.Approximately(dx, 0f)) {
                return 0f;
            }

            Vector2 dir = dx > 0f ? Vector2.right : Vector2.left;
            float requested = Mathf.Abs(dx);

            float castDistance = requested + SkinWidth;
            float hit = ClampDistance(rb, dir, castDistance, mask, exclude, buffer);
            float allowed = hit < castDistance
                ? Mathf.Clamp(hit - SkinWidth, 0f, requested)
                : requested;

            return allowed * Mathf.Sign(dx);
        }

        /// <summary>
        /// Casts <paramref name="rb"/>'s colliders along <paramref name="dir"/> and returns the free
        /// distance (clamped to <paramref name="distance"/>) before the first non-excluded obstacle.
        /// Shared by barrels and the player so the convoy clamps consistently.
        /// <paramref name="ignoreOverlapped"/> skips hits at distance 0 — colliders the body already
        /// overlaps, which Cast reports regardless of direction. Only pass true when the caller has
        /// independently verified there is no real obstacle that close (e.g. the ground rays said
        /// airborne); otherwise a genuinely flush surface would be skipped too.
        /// </summary>
        public static float ClampDistance(
            Rigidbody2D rb,
            Vector2 dir,
            float distance,
            LayerMask mask,
            Collider2D[] exclude,
            RaycastHit2D[] buffer,
            bool ignoreOverlapped = false
        ) {
            if (distance <= 0f) {
                return 0f;
            }

            ContactFilter2D filter = new ContactFilter2D {
                useLayerMask = true,
                layerMask = mask,
                useTriggers = false
            };

            int count = rb.Cast(dir, filter, buffer, distance);
            float allowed = distance;

            for (int i = 0; i < count; i++) {
                Collider2D hitCollider = buffer[i].collider;
                if (hitCollider == null) {
                    continue;
                }

                if (exclude != null && System.Array.IndexOf(exclude, hitCollider) >= 0) {
                    continue;
                }

                // Already-overlapping colliders show up at distance 0 (or less) no matter which
                // way we cast; see the parameter doc for when skipping them is safe.
                if (ignoreOverlapped && buffer[i].distance <= 0f) {
                    continue;
                }

                if (buffer[i].distance < allowed) {
                    allowed = buffer[i].distance;
                }
            }

            return Mathf.Max(allowed, 0f);
        }
    }
}
