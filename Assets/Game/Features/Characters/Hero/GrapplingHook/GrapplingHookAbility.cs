using UnityEngine;

namespace Game.Features.Characters.Hero.GrapplingHook {
    /// <summary>
    /// Main grappling hook component on the Hero prefab. Owns all hook config,
    /// lifecycle, and physics. Follows the same pattern as DragAbility --- a
    /// self-contained MonoBehaviour that only calls minimal methods on
    /// PlayerController (SetHookSwingMode).
    /// </summary>
    public class GrapplingHookAbility : MonoBehaviour {
        private const float ArrivalThreshold = 0.1f;
        private const int MaxAnchorCandidates = 8;

        [Header("Detection")]
        [SerializeField] private float hookRadius = 8f;
        [SerializeField] private LayerMask anchorLayer;

        [Header("Prefabs")]
        [SerializeField] private GameObject hookPrefab;
        [SerializeField] private GameObject ropePrefab;

        [Header("Swing")]
        [SerializeField] private float swingInfluenceForce = 150f;

        public HookState State => fsm.State;

        private PlayerController player;
        private Rigidbody2D playerRb;
        private GrapplingHookFsm fsm;
        private GrapplingHookProjectile activeHook;
        private GrapplingHookRope activeRope;
        private GrapplingHookAnchor targetAnchor;
        private DistanceJoint2D swingJoint;
        private bool isPerkButtonHeld;

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
            return fsm.State == HookState.Idle && FindNearestAnchor() != null;
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

            // Spawn rope visual
            if (ropePrefab != null) {
                var ropeGo = Instantiate(ropePrefab, Vector3.zero, Quaternion.identity);
                activeRope = ropeGo.GetComponent<GrapplingHookRope>();
                activeRope.Initialize(heroPos, anchorPos);
            }

            isPerkButtonHeld = true;
            fsm.Go(HookState.Shooting);
        }

        public void Tick(float deltaTime) {
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
            if (!isPerkButtonHeld) {
                StartRetract();
                return;
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

            swingJoint = player.gameObject.AddComponent<DistanceJoint2D>();
            swingJoint.autoConfigureDistance = false;
            swingJoint.distance = Vector2.Distance(heroPos, anchorPos);
            swingJoint.maxDistanceOnly = true;
            swingJoint.enableCollision = false;
            swingJoint.connectedBody = null;
            swingJoint.connectedAnchor = anchorPos;

            player.SetHookSwingMode(true);

            // Destroy the hook projectile visually — it's now "stuck" at the anchor.
            // Keep the anchor reference for the rope endpoint.
            if (activeHook != null) {
                Destroy(activeHook.gameObject);
                activeHook = null;
            }

            fsm.Go(HookState.Attached);
        }

        private void StartRetract() {
            // Remove swing constraint
            if (swingJoint != null) {
                Destroy(swingJoint);
                swingJoint = null;
            }

            player.SetHookSwingMode(false);

            if (activeHook != null) {
                activeHook.ReturnTo(player.transform);
                fsm.Go(HookState.Retracting);
            } else {
                // Hook was already destroyed (e.g. was in Attached state) — go idle immediately
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
            // Anchor destroyed
            if (targetAnchor == null) {
                return true;
            }

            // Hero took damage
            if (player.Damageable.IsHitThisFrame) {
                return true;
            }

            return false;
        }

        private Vector2 GetHookEndpoint() {
            // While attached, the rope endpoint is the anchor itself (hook was destroyed)
            if (fsm.State == HookState.Attached && targetAnchor != null) {
                return targetAnchor.transform.position;
            }

            // Otherwise it's the projectile position
            if (activeHook != null) {
                return activeHook.Position;
            }

            return GetHeroPosition();
        }

        private Vector2 GetHeroPosition() {
            return player.transform.position;
        }

        private GrapplingHookAnchor FindNearestAnchor() {
            int count = Physics2D.OverlapCircleNonAlloc(
                player.transform.position, hookRadius, anchorCandidates, anchorLayer
            );

            if (count == 0) {
                return null;
            }

            GrapplingHookAnchor nearest = null;
            float nearestSqrDist = float.MaxValue;
            var heroPos = (Vector2)player.transform.position;

            for (int i = 0; i < count; i++) {
                var anchor = anchorCandidates[i].GetComponent<GrapplingHookAnchor>();
                if (anchor == null) {
                    continue;
                }

                float sqrDist = ((Vector2)anchor.transform.position - heroPos).sqrMagnitude;
                if (sqrDist < nearestSqrDist) {
                    nearestSqrDist = sqrDist;
                    nearest = anchor;
                }
            }

            return nearest;
        }

        private void OnDrawGizmosSelected() {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, hookRadius);
        }
    }
}
