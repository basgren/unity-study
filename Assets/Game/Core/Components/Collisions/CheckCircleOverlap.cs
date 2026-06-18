using System.Collections.Generic;
using UnityEngine;

namespace Core.Components.Collisions {
    /// <summary>
    /// Checks for overlapping colliders within a circle and populates a list with the results.
    /// </summary>
    public class CheckCircleOverlap : MonoBehaviour {
        [SerializeField]
        private Vector2 center;
        
        [SerializeField]
        private float radius;

        [SerializeField]
        private string targetTag;
        
        private readonly Collider2D[] overlapTargets = new Collider2D[10];
        
        public bool Check(List<GameObject> targets) {
            // OverlapCircleNonAlloc was deprecated in Unity 6. Replicate its behavior:
            // useTriggers mirrors the old global Physics2D.queriesHitTriggers; DefaultRaycastLayers
            // matches the old no-layerMask overload (excludes Ignore Raycast).
            var filter = new ContactFilter2D { useTriggers = Physics2D.queriesHitTriggers };
            filter.SetLayerMask(Physics2D.DefaultRaycastLayers);
            var size = Physics2D.OverlapCircle(
                transform.position,
                radius,
                filter,
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
