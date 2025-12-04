using System;
using System.Collections.Generic;
using System.Linq;
using PixelCrew.Player;
using UnityEngine;

namespace Components.Abilities {
    public class DragAbility : MonoBehaviour {
        [Header("References")]
        [SerializeField]
        private Rigidbody2D playerRb;

        [SerializeField]
        private Collider2D playerCollider;

        [SerializeField]
        private Transform interactPoint; // точка перед игроком

        [SerializeField]
        private float interactRadius = 0.5f;

        [SerializeField]
        private LayerMask barrelLayer;

        [Header("Dragging")]
        [SerializeField]
        private float dragSpeedMultiplier = 0.4f;

        private PlayerController player;
        private FixedJoint2D dragJoint;
        private DraggableBarrel draggedBottom;
        private DraggableBarrel draggedTop;
                
        private DraggableBarrel highlightedBarrel;
        private List<DraggableBarrel> barrelsOnTopHighlighted;
        private const int MaxBarrelsOnTop = 1; 

        private void Awake() {
            player = GetComponent<PlayerController>();
        }

        private void Update() {
            if (player == null) {
                return;
            }

            if (draggedBottom != null) {
                bool isInteractReleased = player.Actions.Interact.WasReleasedThisFrame();
                bool isJumpPressed = player.Actions.Jump.WasPressedThisFrame();

                if (isInteractReleased || isJumpPressed || !player.IsGrounded || !draggedBottom.IsGrounded) {
                    StopDragging();
                }
            } else {
                DraggableBarrel baseBarrel = GetBarrelAtInteractPoint();

                if (highlightedBarrel != baseBarrel) {
                    if (highlightedBarrel != null) {
                        highlightedBarrel?.SetHighlighted(BarrelHighlightMode.None);
                        highlightedBarrel = null;
                    }
                    
                    if (baseBarrel != null) {
                        highlightedBarrel = baseBarrel;
                        baseBarrel.SetHighlighted(BarrelHighlightMode.Hover);
                    }
                }
                
                bool interactWasPressed = player.Actions.Interact.WasPressedThisFrame();

                if (interactWasPressed && player.IsGrounded) {
                    TryStartDragging();
                    highlightedBarrel = null;
                }
            }
        }
        
        // very dirty code, as just a proof of concept
        private void TryStartDragging() {
            DraggableBarrel baseBarrel = GetBarrelAtInteractPoint();

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

            player.SetDragMode(true, dragSpeedMultiplier);
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

            foreach (var barrel in barrelsOnTopHighlighted) {
                barrel.SetHighlighted(BarrelHighlightMode.None);
            }
            
            barrelsOnTopHighlighted.Clear();

            if (player != null) {
                player.SetDragMode(false, 1f);
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
    }
}
