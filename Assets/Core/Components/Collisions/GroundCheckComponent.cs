using Core.Utils;
using UnityEngine;

namespace Core.Components.Collisions {
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
        
        public bool IsGrounded => groundChecker.HasCollision;
        public bool IsGroundedThisFrame => groundChecker.HasEnteredCollisionThisFrame;
        
        private MultiRayCaster groundChecker;
        
        private void Awake() {
            groundChecker = MultiRayCaster.CreateGroundChecker(bodyCollider, groundLayerMask)
                .WithRayCount(raysCount);
        }

        private void Update() {
            groundChecker.Update();
        }

        private void OnDrawGizmos() {
            groundChecker?.DrawGizmos();
        }
    }
}
