using UnityEngine;
using Utils;

namespace Components.Collisions {
    public class GroundCheckComponent: MonoBehaviour {
        [SerializeField]
        public LayerMask groundLayerMask;
        
        [SerializeField]
        private int raysCount = 3;
        
        [SerializeField]
        private BoxCollider2D bodyCollider;
        
        public bool IsGrounded => groundChecker.HasCollision;
        
        private MultiRayCaster groundChecker;
        
        private void Awake() {
            groundChecker = new MultiRayCaster(bodyCollider, groundLayerMask)
                .WithRayCount(raysCount)
                .WithDirection(Direction2D.Down);
        }

        private void Update() {
            groundChecker.Update();
        }

        private void OnDrawGizmosSelected() {
            groundChecker?.DrawGizmos();
        }
    }
}
