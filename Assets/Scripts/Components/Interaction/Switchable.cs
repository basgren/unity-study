using UnityEngine;
using UnityEngine.Events;

namespace Components.Interaction {
    
    [System.Serializable]
    public class OnSwitchChangeEvent : UnityEvent<bool> {}
    
    public class Switchable : MonoBehaviour {
        [SerializeField]
        private bool isActive;
        
        [SerializeField]
        private OnSwitchChangeEvent onChange;

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
