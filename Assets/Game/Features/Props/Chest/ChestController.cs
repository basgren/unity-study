using Game.Core.Bootstrap;
using Game.Core.Components.GameObjects;
using UnityEngine;

namespace Game.Features.Props.Chest {
    [RequireComponent(typeof(Animator))]
    public class ChestController : MonoBehaviour {
        private static readonly int IsOpenKey = Animator.StringToHash("isOpen");
        private static readonly int ClosedKey = Animator.StringToHash("Closed");
        private static readonly int OpenedKey = Animator.StringToHash("Opened");

        private Animator animator;
        private bool isCollected;

        private void Awake() {
            animator = GetComponent<Animator>();
        }

        public void ChangeState(bool isOpen) {
            animator.SetBool(IsOpenKey, isOpen);

            if (G.SceneState != null && G.SceneState.IsRestoring) {
                ApplyStateInstant(isOpen);
                isCollected = isOpen;
            }
        }

        // Called by an animation event in Opening.anim.
        public void SpawnLoot() {
            if (isCollected || (G.SceneState != null && G.SceneState.IsRestoring)) {
                return;
            }

            var comp = GetComponent<LootDropper>();
            comp.DropLoot();
            isCollected = true;
        }

        private void ApplyStateInstant(bool isOpen) {
            int stateHash = isOpen ? OpenedKey : ClosedKey;
            animator.Play(stateHash, 0, 1f);
            animator.Update(0f);
        }
    }
}
