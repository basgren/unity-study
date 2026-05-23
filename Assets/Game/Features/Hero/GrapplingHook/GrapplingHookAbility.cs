using UnityEngine;
using UnityEngine.Serialization;

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
        private const string GrabPointObjectName = "GrapplingHookGrabPoint";

        [Header("Detection")]
        [FormerlySerializedAs("hookRadius")]
        [SerializeField, Tooltip("Max rope length. Also the detection radius for anchors — an anchor beyond this distance cannot be grabbed.")]
        private float maxRopeLength = 8f;

        [SerializeField, Range(-180f, 180f), Tooltip("Sector start angle in degrees, relative to facing direction. 0 = forward, +90 = up, -90 = down, ±180 = behind. Mirrors automatically when the hero faces left.")]
        private float startAngle = -90f;

        [SerializeField, Range(-180f, 180f), Tooltip("Sector end angle in degrees, relative to facing direction. Must be greater than startAngle. Defaults -90/+90 reproduce a 180° forward sector.")]
        private float endAngle = 90f;

        [SerializeField] private LayerMask anchorLayer;

        [Header("Prefabs")]
        [SerializeField] private GameObject hookPrefab;
        [SerializeField] private GameObject ropePrefab;

        [Header("Swing")]
        [SerializeField] private float swingInfluenceForce = 80f;

        [SerializeField, Tooltip("Linear drag applied to the player Rigidbody2D while swinging. Higher = swing decays faster.")]
        private float swingLinearDrag = 1.5f;

        [SerializeField, Tooltip("How fast the rope shortens/lengthens when the player presses up/down (units per second).")]
        private float ropeClimbSpeed = 4f;

        [SerializeField, Tooltip("Minimum rope length when the player climbs all the way up. Keep above 0 to avoid degenerate joint state — low values let the hero hang right at the anchor.")]
        private float minRopeLength = 0.2f;

        public HookState State => fsm.State;

        private PlayerController player;
        private Rigidbody2D playerRb;
        private Transform grabPoint;
        private GrapplingHookFsm fsm;
        private GrapplingHookProjectile activeHook;
        private GrapplingHookRope activeRope;
        private GrapplingHookAnchor targetAnchor;
        private GrapplingHookAnchor highlightedAnchor;
        private DistanceJoint2D swingJoint;
        private bool jumpDetachPending;
        private float originalLinearDrag;
        // Tracks the player's Health at the moment we last sampled it so OnHealthChanged
        // (which also fires on heals) can be filtered to drops only.
        private float lastObservedPlayerHealth;
        private bool isSubscribedToPlayerHealth;

        private readonly Collider2D[] anchorCandidates = new Collider2D[MaxAnchorCandidates];

        private void Awake() {
            player = GetComponent<PlayerController>();
            playerRb = GetComponent<Rigidbody2D>();
            fsm = new GrapplingHookFsm();

            grabPoint = transform.Find(GrabPointObjectName);
            if (grabPoint == null) {
                Debug.LogWarning(
                    $"{nameof(GrapplingHookAbility)}: child '{GrabPointObjectName}' not found on the hero. " +
                    "Rope and hook will spawn from the hero pivot instead.",
                    this
                );
            }
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
            var grabPos = GetGrabPointPosition();

            // Spawn hook projectile from the grab point so the launch visually starts at the hand.
            var hookGo = Instantiate(hookPrefab, grabPos, Quaternion.identity);
            activeHook = hookGo.GetComponent<GrapplingHookProjectile>();
            activeHook.LaunchToward(anchorPos);

            // Spawn rope visual — all points start collapsed at the grab point, extend as hook travels.
            if (ropePrefab != null) {
                var ropeGo = Instantiate(ropePrefab, Vector3.zero, Quaternion.identity);
                activeRope = ropeGo.GetComponent<GrapplingHookRope>();
                activeRope.Initialize(grabPos, maxRopeLength);
            }

            fsm.Go(HookState.Shooting);
        }

        public void Tick(float deltaTime) {
            UpdateAnchorHighlight();

            if (fsm.State == HookState.Idle) {
                return;
            }

            // Forced abort: hero hit, grounded, or anchor destroyed during active states
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
            }

            // Update rope visual endpoints
            if (activeRope != null) {
                activeRope.UpdateEndpoints(GetHookEndpoint(), GetGrabPointPosition());
            }
        }

        // --- State handlers ---

        private void UpdateShooting() {
            if (activeHook == null || !activeHook.HasArrived) {
                return;
            }

            // targetAnchor is guaranteed non-null here — ShouldForceAbort bails
            // out earlier if it was destroyed mid-flight.
            AttachToAnchor();
        }

        private void UpdateAttached() {
            // Deferred detach: the jump was detected last frame, CheckJump already
            // fired the jump with swing momentum — now clean up the hook.
            if (jumpDetachPending) {
                jumpDetachPending = false;
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

            var dir = player.Actions.Move.ReadValue<Vector2>();

            // Up/down climbs the rope: up pulls hero closer, down extends up to max.
            if (swingJoint != null && Mathf.Abs(dir.y) > 0.1f) {
                float delta = -dir.y * ropeClimbSpeed * Time.deltaTime;
                float newLength = Mathf.Clamp(swingJoint.distance + delta, minRopeLength, maxRopeLength);
                swingJoint.distance = newLength;
                activeRope?.LockLength(newLength);
            }

            // Apply swing force from horizontal input
            if (Mathf.Abs(dir.x) > 0.1f) {
                playerRb.AddForce(new Vector2(dir.x * swingInfluenceForce, 0f));
            }
        }

        // --- Attach / detach ---

        private void AttachToAnchor() {
            var anchorPos = (Vector2)targetAnchor.transform.position;
            var grabPos = GetGrabPointPosition();
            // Clamp to max rope length in case the anchor was barely inside detection range.
            float ropeLength = Mathf.Min(Vector2.Distance(grabPos, anchorPos), maxRopeLength);

            swingJoint = player.gameObject.AddComponent<DistanceJoint2D>();
            swingJoint.autoConfigureDistance = false;
            swingJoint.distance = ropeLength;
            swingJoint.maxDistanceOnly = true;
            swingJoint.enableCollision = false;
            swingJoint.connectedBody = null;
            swingJoint.connectedAnchor = anchorPos;
            // Pin the joint's player-side anchor to the grab point in player local space
            // so the physics swing pivot matches the visual rope attachment.
            if (grabPoint != null) {
                swingJoint.anchor = player.transform.InverseTransformPoint(grabPoint.position);
            }

            // Lock rope visual length so it stops being elastic
            if (activeRope != null) {
                activeRope.LockLength(ropeLength);
            }

            // Apply swing drag; restored in CleanupAndGoIdle.
            originalLinearDrag = playerRb.drag;
            playerRb.drag = swingLinearDrag;

            player.SetHookSwingMode(true);

            // Destroy the hook projectile visually — it's now "stuck" at the anchor.
            if (activeHook != null) {
                Destroy(activeHook.gameObject);
                activeHook = null;
            }

            // Subscribe to player health to drop the rope synchronously when damage lands.
            // OnHealthChanged fires inside ApplyDamage, so we react before the next Update tick
            // and before LateUpdate clears Damageable.IsHitThisFrame.
            if (player.Damageable != null) {
                lastObservedPlayerHealth = player.Damageable.Health;
                player.Damageable.OnHealthChanged += OnPlayerHealthChanged;
                isSubscribedToPlayerHealth = true;
            }

            fsm.Go(HookState.Attached);
        }

        private void OnPlayerHealthChanged(float newHealth) {
            // Heals also fire this event; only react to drops.
            if (newHealth < lastObservedPlayerHealth && fsm.State != HookState.Idle) {
                CleanupAndGoIdle();
            }
            lastObservedPlayerHealth = newHealth;
        }

        private void CleanupAndGoIdle() {
            if (isSubscribedToPlayerHealth && player.Damageable != null) {
                player.Damageable.OnHealthChanged -= OnPlayerHealthChanged;
            }

            isSubscribedToPlayerHealth = false;

            if (swingJoint != null) {
                playerRb.drag = originalLinearDrag;
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

            return GetGrabPointPosition();
        }

        /// <summary>
        /// World-space position of the rope's hero-side attachment. Returns the
        /// <c>GrapplingHookGrabPoint</c> child if present, otherwise the hero pivot.
        /// </summary>
        private Vector2 GetGrabPointPosition() {
            return grabPoint != null ? (Vector2)grabPoint.position : (Vector2)player.transform.position;
        }

        private GrapplingHookAnchor FindNearestAnchor() {
            int count = Physics2D.OverlapCircleNonAlloc(
                player.transform.position, maxRopeLength, anchorCandidates, anchorLayer
            );

            if (count == 0) {
                return null;
            }

            int facingSign = player.GetFacingDirSign() >= 0 ? 1 : -1;
            var heroPos = (Vector2)player.transform.position;

            GrapplingHookAnchor nearest = null;
            float nearestSqrDist = float.MaxValue;

            for (int i = 0; i < count; i++) {
                var anchor = anchorCandidates[i].GetComponent<GrapplingHookAnchor>();
                if (anchor == null) {
                    continue;
                }

                var toAnchor = (Vector2)anchor.transform.position - heroPos;

                // Compute the anchor's angle in facing-mirrored local space:
                // 0 = forward, +90 = up, -90 = down, ±180 = behind. Multiplying
                // x by facingSign mirrors the angle automatically when facing left
                // so the sector always sits in the same hero-relative orientation.
                float angle = Mathf.Atan2(toAnchor.y, toAnchor.x * facingSign) * Mathf.Rad2Deg;
                if (angle < startAngle || angle > endAngle) {
                    continue;
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
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.4f);

            float sweep = endAngle - startAngle;
            if (sweep <= 0f) {
                return;
            }

            if (sweep >= 360f) {
                Gizmos.DrawWireSphere(center, maxRopeLength);
                return;
            }

            // Default to right-facing in the editor so the gizmo is visible without play mode.
            int facingSign = 1;
            if (Application.isPlaying && player != null) {
                facingSign = player.GetFacingDirSign() >= 0 ? 1 : -1;
            }

            var startEdge = LocalAngleToDir(startAngle, facingSign);
            var endEdge = LocalAngleToDir(endAngle, facingSign);

            var prevPoint = center + startEdge * maxRopeLength;
            for (int i = 1; i <= GizmoArcSegments; i++) {
                float t = (float)i / GizmoArcSegments;
                float angle = startAngle + sweep * t;
                var point = center + LocalAngleToDir(angle, facingSign) * maxRopeLength;
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }

            Gizmos.DrawLine(center, center + startEdge * maxRopeLength);
            Gizmos.DrawLine(center, center + endEdge * maxRopeLength);
        }

        private static Vector2 LocalAngleToDir(float localAngleDeg, int facingSign) {
            float rad = localAngleDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad) * facingSign, Mathf.Sin(rad));
        }
    }
}
