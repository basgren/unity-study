using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Components {
    [RequireComponent(typeof(Collider2D))]
    public class LayerCheck : MonoBehaviour {
        [SerializeField]
        private LayerMask layers;

        [SerializeField]
        private bool useTriggers;

        private Collider2D myCollider;
        private ContactFilter2D filter;
        
        private List<Collider2D> results;

        private void Awake() {
            myCollider = GetComponent<Collider2D>();
            results = new List<Collider2D>();
            RebuildFilter();
        }
        
        private void RebuildFilter() {
            filter = default;
            filter.useLayerMask = true;
            filter.layerMask = layers;
            filter.useTriggers = useTriggers;
        }

        public bool IsColliding() {
            if (layers.value == 0 || !myCollider.enabled) {
                return false;
            }

            var hitCount = myCollider.OverlapCollider(filter, results);
            
            Debug.Log($">>> Hit Count: {hitCount}");
            if (hitCount > 0) {
                Debug.Log($">>> Hit {results.Count}: {results[0].name}");
            }

            for (var i = 0; i < hitCount; i++) {
                var other = results[i];
                
                if (other != null && other != myCollider) {
                    return true;
                }
            }

            return false;
        }
    }
}
