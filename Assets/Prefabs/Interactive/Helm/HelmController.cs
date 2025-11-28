using UnityEngine;

namespace Prefabs.Interactive.Helm {

    static class HelmAnimationKeys {
        public static readonly int OnUse = Animator.StringToHash("onUse");        
    }

    [RequireComponent(typeof(Animator))]
    public class HelmController: MonoBehaviour {
        private Animator animator;

        void Awake() {
            animator = GetComponent<Animator>();
        }

        public void OnInteract() {
            animator.SetTrigger(HelmAnimationKeys.OnUse);
        }
    }
}
