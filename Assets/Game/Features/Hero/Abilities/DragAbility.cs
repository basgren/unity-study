using System.Collections.Generic;
using System.Linq;
using Game.Components.Abilities;
using Game.Features.Characters.Hero;
using Game.Features.Characters.Hero.Interaction;
using Game.Features.Dynamic;
using Game.Features.Props.Barrel;
using UnityEngine;
using UnityEngine.Localization;

namespace Game.Features.Hero.Abilities {
    /// <summary>
    /// Drives the "drag a barrel" interaction. While a drag is active this component owns the
    /// horizontal motion of a small convoy — the player plus the dragged barrel(s) — moving them
    /// in lockstep at a controlled speed. There are no physics joints: the link is logical, so it
    /// survives a fall and never breaks under force.
    /// </summary>
    public class DragAbility : MonoBehaviour, IInteractionProvider {
        private const int BarrelInteractionPriority = 100;
        private const float InputDeadZone = 0.1f;
        // Grace period the player may be off the ground before a drag is dropped, so one-frame
        // ground-check flickers (gaps between barrel colliders) don't end it.
        private const float GroundLossGrace = 0.15f;

        [Header("References")]
        [SerializeField]
        private Rigidbody2D playerRb;

        [SerializeField]
        private Collider2D playerCollider;

        [SerializeField]
        private Transform interactPoint; // GrabPoint child on the Hero prefab

        [SerializeField]
        private float interactRadius = 0.5f;

        [SerializeField]
        private LayerMask barrelLayer;

        [Header("Dragging")]
        [Tooltip("Drag speed for a single barrel, in units per second (1 unit = 1 block).")]
        [SerializeField]
        private float dragSpeedSingle = 3f;

        [Tooltip("Drag speed for a two-barrel stack, in units per second.")]
        [SerializeField]
        private float dragSpeedStacked = 2f;

        [Header("Hint")]
        [Tooltip("Localized verb shown by the HUD interaction hint when a barrel is the selected target.")]
        [SerializeField]
        private LocalizedString actionText;

        public Transform InteractPoint => interactPoint;

        private PlayerController player;
        private DraggableBarrel draggedBottom;
        private DraggableBarrel draggedTop;

        // The barrels' colliders, excluded from convoy casts so members don't block each other / the player.
        private Collider2D[] convoyColliders;
        // True when the grabbed stack is too heavy to move (3+ barrels): the drag holds but nothing moves.
        private bool dragLocked;
        // Time the player has been off the ground during the current drag (coyote grace).
        private float playerAirborneTime;

        // Reused cast buffer for the player clamp.
        private readonly RaycastHit2D[] playerCastBuffer = new RaycastHit2D[8];

        // Reused overlap buffer for picking the barrel under the grab point.
        private readonly Collider2D[] barrelOverlapBuffer = new Collider2D[8];

        private DraggableBarrel highlightedBarrel;
        private List<DraggableBarrel> barrelsOnTopHighlighted;
        private const int MaxBarrelsOnTop = 1;

        private BarrelDragHandle activeHandle;

        // Pooled candidate adapter — reused frame-to-frame to avoid GC churn in Update.
        private BarrelDragCandidate cachedCandidate;

        private void Awake() {
            player = GetComponent<PlayerController>();
        }

        private void Update() {
            if (player == null) {
                return;
            }

            // While a drag is in progress, this component owns the lifecycle.
            // The modal handle keeps the resolver suspended until StopDragging() runs.
            // NOTE: a dragged barrel going airborne (e.g. pushed off a ledge) does NOT end the
            // drag — it is allowed to fall and the drag auto-continues on landing (see FixedUpdate).
            if (draggedBottom != null) {
                bool isInteractReleased = player.Actions.Interact.WasReleasedThisFrame();
                bool isJumpPressed = player.Actions.Jump.WasPressedThisFrame();

                // Tolerate a brief loss of footing (coyote-style): only a sustained ground loss
                // (the player actually walked off and is falling) ends the drag, not a one-frame
                // ground-check flicker.
                if (player.IsGrounded) {
                    playerAirborneTime = 0f;
                } else {
                    playerAirborneTime += Time.deltaTime;
                }

                if (isInteractReleased || isJumpPressed || playerAirborneTime > GroundLossGrace) {
                    StopDragging();
                }
            }
        }

        private void FixedUpdate() {
            if (draggedBottom == null) {
                return;
            }

            bool grounded = draggedBottom.IsGrounded;

            // Reach is evaluated only once the barrel has SETTLED (grounded). While it is mid-Y-change
            // (falling off an edge, or riding over a bump) the drag is kept alive — we do not judge
            // reach mid-fall. Once it settles: if the grab point still intersects the barrel the drag
            // resumes automatically (Interact is still held); if it has dropped out of reach, stop.
            if (grounded && !IsWithinReach(draggedBottom)) {
                StopDragging();
                return;
            }

            // Too heavy: the barrels are not dragged (they settle via their own motor); hold
            // the player still so the resistance reads.
            if (dragLocked) {
                SetPlayerVelocityX(0f);
                return;
            }

            float dt = Time.fixedDeltaTime;
            float allowedDx = 0f;

            if (grounded) {
                float dir = ReadDragInputDir();
                if (!Mathf.Approximately(dir, 0f)) {
                    float speed = draggedTop != null ? dragSpeedStacked : dragSpeedSingle;
                    float desiredDx = dir * speed * dt;

                    // Lockstep clamp: the convoy moves only as far as its most-blocked member.
                    float barrelAllowed = draggedBottom.Motor.ClampHorizontal(desiredDx, convoyColliders);
                    float playerAllowed = ClampPlayer(desiredDx);
                    allowedDx = Mathf.Sign(desiredDx) * Mathf.Min(Mathf.Abs(barrelAllowed), Mathf.Abs(playerAllowed));
                }

                SetPlayerVelocityX(allowedDx / dt);
            } else {
                // Rule: a falling barrel cannot be pushed. allowedDx stays 0, so the barrel only
                // drops straight down; hold the player still while it falls.
                SetPlayerVelocityX(0f);
            }

            // Drive the bottom barrel: horizontal step when grounded, gravity when airborne.
            draggedBottom.Motor.DragStep(allowedDx);

            // Carry the top barrel. It moves as far as its own obstacles allow: if something blocks
            // it, it simply stays put (carried at 0) while the bottom slides under it — it is NOT
            // detached just for being blocked. It is released only once the bottom has slid out from
            // under it (no longer resting on the bottom), at which point its own motor lets it fall.
            if (draggedTop != null) {
                float topAllowed = draggedTop.Motor.ClampHorizontal(allowedDx, convoyColliders);
                draggedTop.Motor.DragStep(topAllowed);

                if (!IsTopRestingOnBottom()) {
                    ReleaseTopBarrel();
                }
            }
        }

        public void CollectCandidates(List<IInteractionCandidate> output) {
            // Defensive: while a drag is active the resolver is suspended anyway.
            if (activeHandle != null && activeHandle.IsActive) {
                return;
            }

            DraggableBarrel barrel = GetBarrelAtInteractPoint();
            if (barrel == null) {
                return;
            }

            if (!player.IsGrounded) {
                return;
            }

            if (cachedCandidate == null) {
                cachedCandidate = new BarrelDragCandidate(this);
            }
            cachedCandidate.Refresh(barrel, interactPoint.position);
            output.Add(cachedCandidate);
        }

        private void OnCandidateHoverEnter(DraggableBarrel barrel) {
            if (highlightedBarrel == barrel) {
                return;
            }

            if (highlightedBarrel != null) {
                highlightedBarrel.SetHighlighted(BarrelHighlightMode.None);
            }

            highlightedBarrel = barrel;
            if (highlightedBarrel != null) {
                highlightedBarrel.SetHighlighted(BarrelHighlightMode.Hover);
            }
        }

        private void OnCandidateHoverExit(DraggableBarrel barrel) {
            if (highlightedBarrel == barrel && highlightedBarrel != null) {
                highlightedBarrel.SetHighlighted(BarrelHighlightMode.None);
                highlightedBarrel = null;
            }
        }

        private IInteractionHandle BeginDrag(DraggableBarrel barrel) {
            // Clear hover state — the drag itself owns the visual now.
            highlightedBarrel = null;

            TryStartDragging(barrel);

            if (draggedBottom == null) {
                return null;
            }

            activeHandle = new BarrelDragHandle();
            return activeHandle;
        }

        private void TryStartDragging(DraggableBarrel baseBarrel) {
            if (baseBarrel == null) {
                return;
            }

            int aboveCountSorted = CountBarrelsAboveSorted(baseBarrel, out var topBarrelsSorted);

            barrelsOnTopHighlighted = topBarrelsSorted;
            draggedBottom = baseBarrel;
            draggedTop = topBarrelsSorted.Count > 0 ? topBarrelsSorted[0] : null;
            dragLocked = aboveCountSorted > MaxBarrelsOnTop;

            draggedBottom.SetHighlighted(BarrelHighlightMode.Interact);
            draggedBottom.SetDragged(!dragLocked);

            if (draggedTop != null) {
                draggedTop.SetHighlighted(BarrelHighlightMode.Interact);
                draggedTop.SetDragged(!dragLocked);
            }

            for (int i = 1; i < barrelsOnTopHighlighted.Count; i++) {
                barrelsOnTopHighlighted[i].SetHighlighted(BarrelHighlightMode.Alert);
            }

            if (dragLocked) {
                // Too heavy: don't carry the top barrel; the convoy stays put so the player
                // sees the alert highlight and feels the resistance.
                draggedTop = null;
            }

            RebuildConvoyColliders();
            playerAirborneTime = 0f;

            player.SetDragMode(true, draggedBottom.transform.position.x);
        }

        private DraggableBarrel GetBarrelAtInteractPoint() {
            int count = Physics2D.OverlapCircleNonAlloc(
                interactPoint.position,
                interactRadius,
                barrelOverlapBuffer,
                barrelLayer
            );

            Vector2 origin = interactPoint.position;
            DraggableBarrel nearest = null;
            float nearestSqr = float.MaxValue;

            // Pick the grounded barrel whose collider is closest to the grab point. With two
            // barrels adjacent (e.g. one just pushed against another), this grabs the one under
            // the grab point rather than an arbitrary overlap hit.
            for (int i = 0; i < count; i++) {
                Collider2D hit = barrelOverlapBuffer[i];
                var barrel = hit.GetComponent<DraggableBarrel>();

                if (barrel == null || !barrel.IsGrounded) {
                    continue;
                }

                float sqr = ((Vector2)hit.ClosestPoint(origin) - origin).sqrMagnitude;
                if (sqr < nearestSqr) {
                    nearestSqr = sqr;
                    nearest = barrel;
                }
            }

            return nearest;
        }

        private void StopDragging() {
            if (draggedBottom != null) {
                draggedBottom.SetDragged(false);
                draggedBottom.SetHighlighted(BarrelHighlightMode.None);
                draggedBottom = null;
            }

            if (draggedTop != null) {
                draggedTop.SetDragged(false);
                draggedTop = null;
            }

            if (barrelsOnTopHighlighted != null) {
                foreach (var barrel in barrelsOnTopHighlighted) {
                    barrel.SetHighlighted(BarrelHighlightMode.None);
                }

                barrelsOnTopHighlighted.Clear();
            }

            convoyColliders = null;
            dragLocked = false;

            if (player != null) {
                SetPlayerVelocityX(0f);
                player.SetDragMode(false, 0f);
            }

            // Release the modal handle so the resolver resumes normal candidate
            // evaluation. Must be the LAST step so any cleanup above runs first.
            if (activeHandle != null) {
                activeHandle.Release();
                activeHandle = null;
            }
        }

        // True while the top barrel still rests on the bottom one (detected by the bottom's upward
        // stacking check). Once the bottom slides out from under it, this returns false and the top
        // is released to fall.
        private bool IsTopRestingOnBottom() {
            if (draggedTop == null) {
                return false;
            }

            return draggedBottom.GetDraggablesAbove<DraggableBarrel>().Contains(draggedTop);
        }

        private void ReleaseTopBarrel() {
            if (draggedTop == null) {
                return;
            }

            draggedTop.SetDragged(false);
            draggedTop.SetHighlighted(BarrelHighlightMode.None);
            draggedTop = null;
            RebuildConvoyColliders();
        }

        private void RebuildConvoyColliders() {
            var colliders = new List<Collider2D>();
            // The player is part of the convoy: it must never block the barrel's movement cast.
            // Without this, pulling fails — the player leads the pull and the barrel's cast in the
            // pull direction runs straight into the player (while pushing works because the player
            // trails behind).
            if (playerCollider != null) {
                colliders.Add(playerCollider);
            }
            if (draggedBottom != null) {
                colliders.AddRange(draggedBottom.BodyColliders);
            }
            if (draggedTop != null) {
                colliders.AddRange(draggedTop.BodyColliders);
            }
            convoyColliders = colliders.ToArray();
        }

        private float ReadDragInputDir() {
            float x = player.Actions.Move.ReadValue<Vector2>().x;
            if (x > InputDeadZone) {
                return 1f;
            }
            if (x < -InputDeadZone) {
                return -1f;
            }
            return 0f;
        }

        private float ClampPlayer(float desiredDx) {
            // Same skin-aware clamp the barrel uses: the player stops a sub-pixel short of an
            // obstacle (e.g. another barrel) instead of wedging flush against it. Wedging flush
            // would make the player's cast report that obstacle at distance 0 in every direction,
            // trapping the player so it can't even reverse out.
            return BarrelMotor.ClampMoveWithSkin(
                playerRb, desiredDx, draggedBottom.ObstacleMask, convoyColliders, playerCastBuffer);
        }

        private void SetPlayerVelocityX(float vx) {
            if (playerRb != null) {
                playerRb.velocity = new Vector2(vx, playerRb.velocity.y);
            }
        }

        // The drag is kept only while the grab point's circle actually intersects the barrel
        // collider — the same overlap test used to grab it. Once the barrel has dropped away (its
        // collider no longer within interactRadius of the grab point), the drag releases. The
        // settle (BarrelMotor) keeps a held barrel from hopping a pixel, so this tight test does
        // not drop a drag spuriously during normal movement.
        private bool IsWithinReach(DraggableBarrel barrel) {
            Vector2 closest = barrel.Collider.ClosestPoint(interactPoint.position);
            return ((Vector2)interactPoint.position - closest).sqrMagnitude <= interactRadius * interactRadius;
        }

        // Returns the number of barrels above up to 2nd level. Also returns a sorted array of barrels above
        // baseBarrel. The first element is the nearest one. Max 2 levels checked,
        // so all barrels in the output array starting from index 1 are preventing dragging.
        private int CountBarrelsAboveSorted(
            DraggableBarrel baseBarrel,
            out List<DraggableBarrel> topBarrelsSorted,
            int maxLevels = 2
        ) {
            var result = new HashSet<DraggableBarrel>();

            Queue<DraggableBarrel> queue = new Queue<DraggableBarrel>();
            queue.Enqueue(baseBarrel);
            var level = 0;

            while (queue.Count > 0 && level < maxLevels) {
                var barrel = queue.Dequeue();
                var barrelsOnTop = barrel.GetDraggablesAbove<DraggableBarrel>();

                foreach (var barrelOnTop in barrelsOnTop) {
                    queue.Enqueue(barrelOnTop);
                    result.Add(barrelOnTop);
                }

                level++;
            }

            List<DraggableBarrel> barrels = result.ToList();

            barrels.Sort((barr1, barr2) => {
                var dist1 = (barr1.transform.position - interactPoint.transform.position).sqrMagnitude;
                var dist2 = (barr2.transform.position - interactPoint.transform.position).sqrMagnitude;

                return dist1.CompareTo(dist2);
            });

            topBarrelsSorted = barrels;

            return barrels.Count;
        }

        private void OnDrawGizmosSelected() {
            if (interactPoint != null) {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(interactPoint.position, interactRadius);
            }
        }

        private class BarrelDragCandidate : IInteractionCandidate {
            private readonly DragAbility ability;
            private DraggableBarrel barrel;
            private float sqrDistance;

            public BarrelDragCandidate(DragAbility ability) {
                this.ability = ability;
            }

            public void Refresh(DraggableBarrel barrel, Vector3 referencePoint) {
                this.barrel = barrel;
                sqrDistance = (barrel.transform.position - referencePoint).sqrMagnitude;
            }

            public int Priority => BarrelInteractionPriority;
            public LocalizedString ActionText => ability.actionText;
            public float SqrDistanceFromGrabPoint => sqrDistance;
            public bool IsValid => barrel != null && barrel.IsGrounded;
            public int StableId => barrel != null ? barrel.GetInstanceID() : 0;

            public void OnHoverEnter() {
                ability.OnCandidateHoverEnter(barrel);
            }

            public void OnHoverExit() {
                ability.OnCandidateHoverExit(barrel);
            }

            public IInteractionHandle Execute() {
                return ability.BeginDrag(barrel);
            }
        }
    }
}
