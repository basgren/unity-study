using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Prefabs.Props.TrainingDummy {
    public class TrainingDummyController : MonoBehaviour {
        private Animator anim;
        
        private void Awake() {
            anim = GetComponent<Animator>();
        }

        public void OnHit() {
            anim.SetTrigger(TrainingDummyAnimKeys.GetRandomHitKey());
        }
        
        private static class TrainingDummyAnimKeys {
            public static readonly int Hit1 = Animator.StringToHash("hit1");
            public static readonly int Hit2 = Animator.StringToHash("hit2");
            public static readonly int Hit3 = Animator.StringToHash("hit3");

            public static int GetRandomHitKey() { 
                int hitId = Random.Range(0, 3);

                switch (hitId) {
                    case 1: return Hit2;
                    case 2: return Hit3;
                    default: return Hit1;
                }
            }
        }
    }
}
