using System;
using Core;
using Core.Utils;
using Game.Core.Utils;
using UnityEngine;

namespace Game.Components.Abilities {
    [RequireComponent(typeof(Rigidbody2D))]
    public class WalkUpstairsAbility : MonoBehaviour {
        [SerializeField]
        private BoxCollider2D bodyCollider;
        
        [SerializeField]
        private float maxStairHeight = 3f * CoreConst.PixelSize;
        
        [SerializeField]
        private LayerMask groundLayer;

        private Rigidbody2D rb;
        
        private void Awake() {
            rb = GetComponent<Rigidbody2D>();
        }

        private void FixedUpdate() {
            bool hasStairToStepOn = DetectStairAlongTheWay(out Vector2 stepDistance);

            if (hasStairToStepOn) {
                // Debug.Log($"StepUp OK dir={stepDistance}");
                rb.MovePosition(rb.position + stepDistance);
            }
        }

        private bool DetectStairAlongTheWay(out Vector2 stepDistance) {
            stepDistance = Vector2.zero;

            if (rb.linearVelocity.x == 0f) {
                return false;
            }
            
            Vector2 moveDir = GetMoveDir();
            Bounds bounds = bodyCollider.bounds;
            var moveDistance = Math.Abs(rb.linearVelocity.x) * Time.fixedDeltaTime;

            float skin = CoreConst.HalfPixelSize;
            float edgeX = moveDir.x > 0f
                ? bounds.max.x + skin
                : bounds.min.x - skin;

            // Minor adjustments to avoid preliminary collisions - mostly with several dynamic
            // objects standing in line when player walks on top of them.
            Vector2 feetOrigin = new Vector2(edgeX, bounds.min.y);

            bool canStep = Geometry.TryFindStepHeight(
                feetOrigin,
                moveDir,
                maxStairHeight,
                CoreConst.HalfPixelSize,
                moveDistance,
                groundLayer,
                out var stepHeight
            );

            if (canStep) {
                stepDistance = new Vector2(
                    moveDir.x * moveDistance,
                    stepHeight + CoreConst.PixelSize // minor adjustment to avoid preliminary collisions
                );
                
                return true;
            }

            return false;
        }

        private Vector2 GetMoveDir() {
            return new Vector2(Mathf.Sign(rb.linearVelocity.x), 0f);
        }
    }
}
