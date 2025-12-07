using System;
using Components.Interaction;
using UnityEngine;

namespace Prefabs.Interactive.Helm {

    static class HelmAnimationKeys {
        public static readonly int OnUse = Animator.StringToHash("onUse");        
        public static readonly int IsDisabled = Animator.StringToHash("isDisabled");        
    }

    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(Switch))]
    public class HelmController: MonoBehaviour {
        private Animator animator;
        private Switch @switch; 

        void Awake() {
            animator = GetComponent<Animator>();
            @switch = GetComponent<Switch>();
        }

        private void Update() {
            UpdateAnimator();
        }

        private void UpdateAnimator() {
            animator.SetBool(HelmAnimationKeys.IsDisabled, @switch.isDisabled);
        }

        public void OnInteract() {
            animator.SetTrigger(HelmAnimationKeys.OnUse);
        }
    }
}
