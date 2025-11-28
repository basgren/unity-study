using Components.Interaction;
using UnityEngine;

namespace Prefabs.Interactive.StoneDoor {
    static class StoneDoorAnimationKeys {
        public static readonly int IsOpen = Animator.StringToHash("isOpen");
    }

    [RequireComponent(typeof(Switchable))]
    [RequireComponent(typeof(Animator))]
    public class StoneDoorController : MonoBehaviour {
        private Animator animator;

        private void Awake() {
            animator = GetComponent<Animator>();
        }

        public void OnChangeState(bool isActive) {
            animator.SetBool(StoneDoorAnimationKeys.IsOpen, isActive);
        }
    }
}
