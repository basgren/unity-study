using Core.Utils;
using UnityEngine;

namespace Game.Core.Components.Collisions {
    /// <summary>
    /// Component that checks if the character is grounded using multiple raycasts.
    /// </summary>
    public class GroundCheckComponent: MonoBehaviour {
        [SerializeField]
        public LayerMask groundLayerMask;
        
        [SerializeField]
        private int raysCount = 3;
        
        [SerializeField]
        private BoxCollider2D bodyCollider;
        
        public bool IsGrounded => groundChecker != null && groundChecker.HasCollision;
        public bool IsGroundedThisFrame => groundChecker != null && groundChecker.HasEnteredCollisionThisFrame;

        private MultiRayCaster groundChecker;

        private void Awake() {
            // Fall back to the collider on this object if the reference was lost (e.g. another
            // collider was removed in the Inspector), so the checker never fails to build.
            if (bodyCollider == null) {
                bodyCollider = GetComponent<BoxCollider2D>();
            }

            groundChecker = MultiRayCaster.CreateGroundChecker(bodyCollider, groundLayerMask)
                .WithRayCount(raysCount);

            groundChecker.Update();
        }

        private void Update() {
            groundChecker?.Update();
        }

        private void OnDrawGizmos() {
            groundChecker?.DrawGizmos();
        }
    }
}
