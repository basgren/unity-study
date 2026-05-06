using System;
using UnityEngine;

namespace Game.Features.Bosses.VengefulSpirit.SpectralShield {
    internal static class SpectralShieldAnimKeys {
        public static readonly int OnDestroy = Animator.StringToHash("onDestroy");
        public static readonly int OnHit = Animator.StringToHash("onHit");
    }
    
    public class SpectralShield : MonoBehaviour {
        
        private Animator anim;
        
        private void Awake() {
            anim = GetComponent<Animator>();
        }

        public void OnHit() {
            anim.SetTrigger(SpectralShieldAnimKeys.OnHit);
        }
        
        public void OnBeforeDestroy() {
            anim.SetTrigger(SpectralShieldAnimKeys.OnDestroy);
        }

        public void OnDestroyAnimExit() {
            Destroy(gameObject);
        }
    }
}
