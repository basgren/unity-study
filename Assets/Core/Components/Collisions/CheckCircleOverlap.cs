using System.Collections.Generic;
using UnityEngine;

namespace Core.Components.Collisions {
    public class CheckCircleOverlap : MonoBehaviour {
        [SerializeField]
        private Vector2 center;
        
        [SerializeField]
        private float radius;

        [SerializeField]
        private string targetTag;
        
        private readonly Collider2D[] overlapTargets = new Collider2D[10];
        
        public bool Check(List<GameObject> targets) {
            var size = Physics2D.OverlapCircleNonAlloc(
                transform.position,
                radius,
                overlapTargets
            );

            targets.Clear();
            
            for (var i = 0; i < size; i++) {
                targets.Add(overlapTargets[i].gameObject);
            }

            return size > 0;
        }
        
        private void OnDrawGizmosSelected() {
            Gizmos.DrawWireSphere(center, radius);
        }
    }
}
