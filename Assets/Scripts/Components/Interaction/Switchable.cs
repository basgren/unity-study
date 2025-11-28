using UnityEngine;
using UnityEngine.Events;

namespace Components.Interaction {
    
    [System.Serializable]
    public class OnSwitchChangeEvent : UnityEvent<bool> {}
    
    public class Switchable : MonoBehaviour {
        
        [SerializeField]
        private OnSwitchChangeEvent onChange;
        
        private bool isActive;
        public bool IsActive {
            get => isActive;
            set {
                if (isActive != value) {
                    isActive = value;
                    onChange?.Invoke(isActive);
                }
            }
        }

        public void Toggle() {
            IsActive = !IsActive;
        }
    }
}
