using System;
using Core.Components;
using UnityEngine;

namespace Prefabs.Props.Chest {
    [RequireComponent(typeof(Animator))]
    public class ChestController : MonoBehaviour {
        private static readonly int IsOpenKey = Animator.StringToHash("isOpen");
        
        private Animator animator;

        private bool isCollected;
        
        private void Awake() {
            animator = GetComponent<Animator>();
        }

        public void ChangeState(bool isOpen) {
            animator.SetBool(IsOpenKey, isOpen);
        }
        
        public void SpawnCoins() {
            if (isCollected) {
                return;
            }

            var comp = GetComponent<LootDropper>();
            comp.DropLoot(100);
            isCollected = true;
        }
    }
}
