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
        
        public bool IsGrounded => groundChecker.IsGrounded;
        
        private GroundChecker groundChecker;
        
        private void Awake() {
            groundChecker = new GroundChecker(bodyCollider, groundLayerMask, raysCount);
        }

        private void Update() {
            groundChecker.Update();
        }

        private void OnDrawGizmosSelected() {
            GroundCheckerUtils.DrawGroundCheckerGizmos(groundChecker);
        }
    }
}
