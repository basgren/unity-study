using System;
using Core.Components.Extensions;
using UnityEngine;
using UnityEngine.Events;

namespace Core.Components.Collisions {
    [RequireComponent(typeof(Collider2D))]
    public class TriggerEnterComponent : MonoBehaviour {
        [SerializeField]
        private string otherTag;

        [SerializeField]
        private LayerMask layer = ~0;
        
        [SerializeField]
        private TriggerEnterEvent action;
        
        private void OnTriggerEnter2D(Collider2D other) {
            if (!layer.Contains(other.gameObject)) {
                return;
            }
            
            if (!string.IsNullOrEmpty(otherTag) && !other.gameObject.CompareTag(otherTag)) {
                return;
            }
            
            if (action != null) {
                action.Invoke(other.gameObject);
            } else {
                Debug.LogWarning("Action is not set");
            }
        }
    }
    
    [Serializable]
    public class TriggerEnterEvent : UnityEvent<GameObject> {}
}
