using UnityEngine;

namespace Game.Features.Characters.Hero.GrapplingHook {
    /// <summary>
    /// Main grappling hook component on the Hero prefab. Owns all hook config,
    /// lifecycle, and physics. Follows the same pattern as DragAbility --- a
    /// self-contained MonoBehaviour that only calls minimal methods on
    /// PlayerController (SetHookSwingMode).
    /// </summary>
    public class GrapplingHookAbility : MonoBehaviour {
        private const int MaxAnchorCandidates = 8;
        private const int GizmoArcSegments = 20;

        [Header("Detection")]
        [SerializeField] private float hookRadius = 8f;

        [SerializeField, Range(10f, 360f), Tooltip("Full sector angle in degrees. 180 = half-circle in front of player.")]
        private float sectorAngle = 180f;

        [SerializeField] private LayerMask anchorLayer;

        [Header("Prefabs")]
        [SerializeField] private GameObject hookPrefab;
        [SerializeField] private GameObject ropePrefab;

        [Header("Swing")]
        [SerializeField] private float swingInfluenceForce = 80f;

        public HookState State => fsm.State;

        private PlayerController player;
        private Rigidbody2D playerRb;
        private GrapplingHookFsm fsm;
        private GrapplingHookProjectile activeHook;
        private GrapplingHookRope activeRope;
        private GrapplingHookAnchor targetAnchor;
        private GrapplingHookAnchor highlightedAnchor;
        private DistanceJoint2D swingJoint;
        private bool isPerkButtonHeld;
        private bool jumpDetachPending;

        private readonly Collider2D[] anchorCandidates = new Collider2D[MaxAnchorCandidates];

        private void Awake() {
            player = GetComponent<PlayerController>();
            playerRb = GetComponent<Rigidbody2D>();
            fsm = new GrapplingHookFsm();
        }

        private void FixedUpdate() {
            if (fsm.State == HookState.Idle) {
                return;
            }

            activeRope?.Simulate(Time.fixedDeltaTime);
        }

        private void LateUpdate() {
            if (fsm.State == HookState.Idle) {
                return;
            }

            activeRope?.Render();
        }

        // --- Public API called by GrapplingHookStrategy ---

        public bool CanActivate() {
            return fsm.State == HookState.Idle
                   && !player.IsGrounded
                   && FindNearestAnchor() != null;
        }

        public void Activate() {
            targetAnchor = FindNearestAnchor();
            if (targetAnchor == null) {
                return;
            }

            var anchorPos = (Vector2)targetAnchor.transform.position;
            var heroPos = (Vector2)player.transform.position;

            // Spawn hook projectile
            var hookGo = Instantiate(hookPrefab, heroPos, Quaternion.identity);
            activeHook = hookGo.GetComponent<GrapplingHookProjectile>();
            activeHook.LaunchToward(anchorPos);

            // Spawn rope visual — all points start at hero, extends as hook travels
            if (ropePrefab != null) {
                var ropeGo = Instantiate(ropePrefab, Vector3.zero, Quaternion.identity);
                activeRope = ropeGo.GetComponent<GrapplingHookRope>();
                activeRope.Initialize(heroPos);
            }

            isPerkButtonHeld = true;
            fsm.Go(HookState.Shooting);
        }

        public void Tick(float deltaTime) {
            UpdateAnchorHighlight();

            if (fsm.State == HookState.Idle) {
                return;
            }

            // Track input release
            if (player.Actions.UsePerk.WasReleasedThisFrame()) {
                isPerkButtonHeld = false;
            }

            // Forced abort: hero hit or anchor destroyed during active states
            if (ShouldForceAbort()) {
                CleanupAndGoIdle();
                return;
            }

            switch (fsm.State) {
                case HookState.Shooting:
                    UpdateShooting();
                    break;
                case HookState.Attached:
                    UpdateAttached();
                    break;
                case HookState.Retracting:
                    UpdateRetracting();
                    break;
            }

            // Update rope visual endpoints
            if (activeRope != null) {
                activeRope.UpdateEndpoints(GetHookEndpoint(), GetHeroPosition());
            }
        }

        // --- State handlers ---

        private void UpdateShooting() {
            if (activeHook == null || !activeHook.HasArrived) {
                return;
            }

            if (isPerkButtonHeld && targetAnchor != null) {
                AttachToAnchor();
            } else {
                StartRetract();
            }
        }

        private void UpdateAttached() {
            // Deferred detach: the jump was detected last frame, CheckJump already
            // fired the jump with swing momentum — now clean up the hook.
            if (jumpDetachPending) {
                jumpDetachPending = false;
                CleanupAndGoIdle();
                return;
            }

            if (!isPerkButtonHeld) {
                CleanupAndGoIdle();
                return;
            }

            // Jump pressed: defer detach to next frame so CheckJump (which runs
            // after Tick in the same Update) can fire the jump while isHookSwinging
            // is still true, preserving swing velocity.
            if (player.Actions.Jump.WasPerformedThisFrame()) {
                jumpDetachPending = true;
                return;
            }

            // Shorten rope as player gets closer to anchor (self-tightening)
            if (swingJoint != null) {
                float currentDist = Vector2.Distance(playerRb.position, swingJoint.connectedAnchor);
                if (currentDist < swingJoint.distance) {
                    swingJoint.distance = currentDist;
                    activeRope?.LockLength(currentDist);
                }
            }

            // Apply swing force from horizontal input
            var dir = player.Actions.Move.ReadValue<Vector2>();
            if (Mathf.Abs(dir.x) > 0.1f) {
                playerRb.AddForce(new Vector2(dir.x * swingInfluenceForce, 0f));
            }
        }

        private void UpdateRetracting() {
            if (activeHook != null && activeHook.HasReturned) {
                CleanupAndGoIdle();
            }
        }

        // --- Attach / detach ---

        private void AttachToAnchor() {
            var anchorPos = (Vector2)targetAnchor.transform.position;
            var heroPos = (Vector2)player.transform.position;
            float ropeLength = Vector2.Distance(heroPos, anchorPos);

            swingJoint = player.gameObject.AddComponent<DistanceJoint2D>();
            swingJoint.autoConfigureDistance = false;
            swingJoint.distance = ropeLength;
            swingJoint.maxDistanceOnly = true;
            swingJoint.enableCollision = false;
            swingJoint.connectedBody = null;
            swingJoint.connectedAnchor = anchorPos;

            // Lock rope visual length so it stops being elastic
            if (activeRope != null) {
                activeRope.LockLength(ropeLength);
            }

            player.SetHookSwingMode(true);

            // Destroy the hook projectile visually — it's now "stuck" at the anchor.
            if (activeHook != null) {
                Destroy(activeHook.gameObject);
                activeHook = null;
            }

            fsm.Go(HookState.Attached);
        }

        private void StartRetract() {
            if (swingJoint != null) {
                Destroy(swingJoint);
                swingJoint = null;
            }

            player.SetHookSwingMode(false);

            if (activeHook != null) {
                activeHook.ReturnTo(player.transform);
                fsm.Go(HookState.Retracting);
            } else {
                CleanupAndGoIdle();
            }
        }

        private void CleanupAndGoIdle() {
            if (swingJoint != null) {
                Destroy(swingJoint);
                swingJoint = null;
            }

            if (activeHook != null) {
                Destroy(activeHook.gameObject);
                activeHook = null;
            }

            if (activeRope != null) {
                activeRope.Cleanup();
                activeRope = null;
            }

            player.SetHookSwingMode(false);
            targetAnchor = null;
            isPerkButtonHeld = false;
            fsm.ResetTo(HookState.Idle);
        }

        // --- Helpers ---

        private bool ShouldForceAbort() {
            if (targetAnchor == null) {
                return true;
            }

            if (player.Damageable.IsHitThisFrame) {
                return true;
            }

            // Landing while the hook is active detaches immediately — same outcome
            // as releasing the use key. Prevents lingering rope after the player
            // swings down onto a platform.
            if (player.IsGrounded) {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Swaps the active-sprite highlight on whichever anchor is the current
        /// hook target. While idle, that's the nearest anchor in the forward sector;
        /// while engaged, it's the locked <see cref="targetAnchor"/>. Only one
        /// anchor is highlighted at a time — the previous one is cleared first.
        /// </summary>
        private void UpdateAnchorHighlight() {
            var desired = fsm.State == HookState.Idle
                ? FindNearestAnchor()
                : targetAnchor;

            if (desired == highlightedAnchor) {
                return;
            }

            if (highlightedAnchor != null) {
                highlightedAnchor.SetHighlighted(false);
            }

            highlightedAnchor = desired;

            if (highlightedAnchor != null) {
                highlightedAnchor.SetHighlighted(true);
            }
        }

        private void OnDestroy() {
            if (highlightedAnchor != null) {
                highlightedAnchor.SetHighlighted(false);
                highlightedAnchor = null;
            }
        }

        private Vector2 GetHookEndpoint() {
            if (fsm.State == HookState.Attached && targetAnchor != null) {
                return targetAnchor.transform.position;
            }

            if (activeHook != null) {
                return activeHook.Position;
            }

            return GetHeroPosition();
        }

        private Vector2 GetHeroPosition() {
            return player.transform.position;
        }

        /// <summary>
        /// Returns the world-space facing direction of the player (right or left).
        /// </summary>
        private Vector2 GetFacingDirection() {
            return player.GetFacingDirSign() >= 0 ? Vector2.right : Vector2.left;
        }

        private GrapplingHookAnchor FindNearestAnchor() {
            int count = Physics2D.OverlapCircleNonAlloc(
                player.transform.position, hookRadius, anchorCandidates, anchorLayer
            );

            if (count == 0) {
                return null;
            }

            float halfAngle = sectorAngle * 0.5f;
            var facing = GetFacingDirection();
            var heroPos = (Vector2)player.transform.position;

            GrapplingHookAnchor nearest = null;
            float nearestSqrDist = float.MaxValue;

            for (int i = 0; i < count; i++) {
                var anchor = anchorCandidates[i].GetComponent<GrapplingHookAnchor>();
                if (anchor == null) {
                    continue;
                }

                var toAnchor = (Vector2)anchor.transform.position - heroPos;

                // Sector check: skip anchors outside the forward sector
                if (sectorAngle < 360f) {
                    float angle = Vector2.Angle(facing, toAnchor);
                    if (angle > halfAngle) {
                        continue;
                    }
                }

                float sqrDist = toAnchor.sqrMagnitude;
                if (sqrDist < nearestSqrDist) {
                    nearestSqrDist = sqrDist;
                    nearest = anchor;
                }
            }

            return nearest;
        }

        // --- Gizmo ---

        private void OnDrawGizmosSelected() {
            DrawSectorGizmo();
        }

        private void DrawSectorGizmo() {
            var center = (Vector2)transform.position;
            var color = new Color(0.2f, 0.8f, 1f, 0.4f);
            Gizmos.color = color;

            if (sectorAngle >= 360f) {
                Gizmos.DrawWireSphere(center, hookRadius);
                return;
            }

            // Determine facing direction — in editor without player, default to right
            var facing = Vector2.right;
            if (Application.isPlaying && player != null) {
                facing = GetFacingDirection();
            }

            float halfAngle = sectorAngle * 0.5f;
            float startAngle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg - halfAngle;

            // Draw arc
            var prevPoint = center + GetArcPoint(startAngle) * hookRadius;
            for (int i = 1; i <= GizmoArcSegments; i++) {
                float t = (float)i / GizmoArcSegments;
                float angle = startAngle + sectorAngle * t;
                var point = center + GetArcPoint(angle) * hookRadius;
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }

            // Draw sector edges
            var edgeStart = center + GetArcPoint(startAngle) * hookRadius;
            var edgeEnd = center + GetArcPoint(startAngle + sectorAngle) * hookRadius;
            Gizmos.DrawLine(center, edgeStart);
            Gizmos.DrawLine(center, edgeEnd);
        }

        private static Vector2 GetArcPoint(float angleDeg) {
            float rad = angleDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }
    }
}
