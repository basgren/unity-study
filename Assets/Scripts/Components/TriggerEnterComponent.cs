using System;
using UnityEngine;
using UnityEngine.Events;

namespace Components {
    [RequireComponent(typeof(Collider2D))]
    public class TriggerEnterComponent : MonoBehaviour {
        [SerializeField]
        private string otherTag;
        
        [SerializeField]
        private TriggerEnterEvent action;
        
        private void OnTriggerEnter2D(Collider2D other) {
            if (other.gameObject.CompareTag(otherTag)) {
                if (action != null) {
                    action.Invoke(other.gameObject);
                } else {
                    Debug.LogWarning("Action is not set");
                }
            }
        }
    }
    
    [Serializable]
    public class TriggerEnterEvent : UnityEvent<GameObject> {}
}
