using System.Collections.Generic;
using System.Linq;
using Game.Components.Abilities;
using Game.Features.Characters.Hero.Interaction;
using Game.Features.Dynamic;
using UnityEngine;
using UnityEngine.Localization;

namespace Game.Features.Characters.Hero.Abilities {
    public class DragAbility : MonoBehaviour, IInteractionProvider {
        private const int BarrelInteractionPriority = 100;

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
        [SerializeField]
        private float dragSpeedMultiplier = 0.4f;

        [Header("Hint")]
        [Tooltip("Localized verb shown by the HUD interaction hint when a barrel is the selected target.")]
        [SerializeField]
        private LocalizedString actionText;

        public Transform InteractPoint => interactPoint;

        private PlayerController player;
        private FixedJoint2D dragJoint;
        private DraggableBarrel draggedBottom;
        private DraggableBarrel draggedTop;

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
            if (draggedBottom != null) {
                bool isInteractReleased = player.Actions.Interact.WasReleasedThisFrame();
                bool isJumpPressed = player.Actions.Jump.WasPressedThisFrame();

                if (isInteractReleased || isJumpPressed || !player.IsGrounded || !draggedBottom.IsGrounded) {
                    StopDragging();
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

        // very dirty code, as just a proof of concept
        private void TryStartDragging(DraggableBarrel baseBarrel) {
            if (baseBarrel == null) {
                return;
            }

            int aboveCountSorted = CountBarrelsAboveSorted(baseBarrel, out var topBarrelsSorted);

            barrelsOnTopHighlighted = topBarrelsSorted;
            draggedBottom = baseBarrel;
            draggedTop = topBarrelsSorted.Count > 0 ? topBarrelsSorted[0] : null;

            draggedBottom.SetHighlighted(BarrelHighlightMode.Interact);
            if (aboveCountSorted <= MaxBarrelsOnTop) {
                draggedBottom.SetDragged(true);
            }

            if (draggedTop != null) {
                draggedTop.SetHighlighted(BarrelHighlightMode.Interact);
                draggedBottom.ConnectToDraggable(draggedTop);

                if (aboveCountSorted <= MaxBarrelsOnTop) {
                    draggedTop.SetDragged(true);
                }
            }

            for (int i = 1; i < barrelsOnTopHighlighted.Count; i++) {
                barrelsOnTopHighlighted[i].SetHighlighted(BarrelHighlightMode.Alert);
            }

            dragJoint = gameObject.AddComponent<FixedJoint2D>();
            dragJoint.connectedBody = draggedBottom.Body;
            dragJoint.enableCollision = true;

            player.SetDragMode(true, draggedBottom.transform.position.x);
        }

        private DraggableBarrel GetBarrelAtInteractPoint() {
            Collider2D hit = Physics2D.OverlapCircle(
                interactPoint.position,
                interactRadius,
                barrelLayer
            );

            if (hit == null) {
                return null;
            }

            var barrel = hit.GetComponent<DraggableBarrel>();

            if (barrel == null || !barrel.IsGrounded) {
                return null;
            }

            return barrel;
        }

        private void StopDragging() {
            if (dragJoint != null) {
                Destroy(dragJoint);
                dragJoint = null;
            }

            if (draggedBottom != null) {
                draggedBottom.SetDragged(false);
                draggedBottom.SetHighlighted(BarrelHighlightMode.None);
                draggedBottom.DisconnectFromDraggable();
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

            if (player != null) {
                player.SetDragMode(false, 0f);
            }

            // Release the modal handle so the resolver resumes normal candidate
            // evaluation. Must be the LAST step so any cleanup above runs first.
            if (activeHandle != null) {
                activeHandle.Release();
                activeHandle = null;
            }
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
