using Components.Interaction;
using UnityEngine;

namespace Prefabs.Interactive.StoneDoor {
    static class StoneDoorAnimationKeys {
        public static readonly int IsOpen = Animator.StringToHash("isOpen");
    }

    [RequireComponent(typeof(Switchable))]
    [RequireComponent(typeof(Animator))]
    public class StoneDoorController : MonoBehaviour {
        private const string ClosedStateName = "Closed";
        private const string OpenStateName = "Opened";
        
        private Animator animator;
        private Switchable switchable;

        private void Awake() {
            animator = GetComponent<Animator>();
            switchable = GetComponent<Switchable>();
            UpdateAnimator(true);
        }

        public void OnChangeState(bool isActive) {
            Debug.Log(">>>> state changed to " + isActive);
            UpdateAnimator();
        }

        private void UpdateAnimator(bool instant = false) {
            animator.SetBool(StoneDoorAnimationKeys.IsOpen, switchable.IsActive);
            
            if (instant) {
                ApplyStateInstant(switchable.IsActive);
            }
        }

        private void ApplyStateInstant(bool isActive) {
            string stateName = isActive ? OpenStateName : ClosedStateName;
            animator.Play(stateName, 0, 1f);
            animator.Update(0f);
        }
    }
}
