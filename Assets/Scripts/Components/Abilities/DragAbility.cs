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
                bool interactWasPressed = player.Actions.Interact.WasPressedThisFrame();

                if (interactWasPressed && player.IsGrounded) {
                    TryStartDragging();
                }
            }
        }
        
        private void TryStartDragging() {
            DraggableBarrel baseBarrel = TryGrabBarrel();

            if (baseBarrel == null) {
                return;
            }

            // Считаем бочки над той, за которую он ухватился
            int aboveCount = CountBarrelsAbove(baseBarrel, out var immediateTop);

            // ЕСЛИ НАД НЕЙ БОЛЬШЕ ОДНОЙ БОЧКИ — НЕ ДАЁМ ПЕРЕТАСКИВАТЬ
            if (aboveCount > 1) {
                return;
            }

            // Всё ок: либо одиночная бочка, либо стек не выше 2 (base + immediateTop)
            draggedBottom = baseBarrel;
            draggedTop = immediateTop;

            draggedBottom.SetDragged(true);
            
            if (draggedTop != null) {
                draggedTop.SetDragged(true);
                draggedBottom.ConnectToDraggable(draggedTop);
            }

            dragJoint = gameObject.AddComponent<FixedJoint2D>();
            dragJoint.connectedBody = draggedBottom.Body;
            dragJoint.enableCollision = true;

            player.SetDragMode(true, dragSpeedMultiplier);
        }

        private DraggableBarrel TryGrabBarrel() {
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
                draggedBottom.DisconnectFromDraggable();
                draggedBottom = null;
            }

            if (draggedTop != null) {
                draggedTop.SetDragged(false);
                draggedTop = null;
            }

            if (player != null) {
                player.SetDragMode(false, 1f);
            }
        }

        private int CountBarrelsAbove(DraggableBarrel baseBarrel, out DraggableBarrel immediateTop) {
            Bounds b = baseBarrel.Collider.bounds;

            // Окно над бочкой — ищем всех, кто реально выше
            Vector2 center = new Vector2(b.center.x, b.max.y + 0.05f);
            Vector2 size = new Vector2(b.size.x * 0.9f, b.size.y * 2f);

            Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f, barrelLayer);

            int count = 0;
            DraggableBarrel nearest = null;
            float nearestBottomY = float.MaxValue;

            foreach (Collider2D hit in hits) {
                if (hit == baseBarrel.Collider) {
                    continue;
                }

                DraggableBarrel other = hit.GetComponent<DraggableBarrel>();
                if (other == null) {
                    continue;
                }

                Bounds ob = other.Collider.bounds;

                // "Выше" — если нижняя грань >= верхней грани базовой
                if (ob.min.y >= b.max.y - 0.01f) {
                    count++;

                    // ищем самую нижнюю из верхних (то есть ближайшую к базовой)
                    if (ob.min.y < nearestBottomY) {
                        nearestBottomY = ob.min.y;
                        nearest = other;
                    }
                }
            }

            immediateTop = nearest;
            return count;
        }

        private void OnDrawGizmosSelected() {
            if (interactPoint != null) {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(interactPoint.position, interactRadius);
            }
        }
    }
}
