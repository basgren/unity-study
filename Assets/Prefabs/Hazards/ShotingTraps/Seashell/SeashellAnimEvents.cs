using UnityEngine;

namespace Prefabs.Characters.Seashell {
    public class SeashellAnimEvents: MonoBehaviour {

        private SeashellController ctrl;

        private void Awake() {
            ctrl = GetComponentInParent<SeashellController>();
        }

        public void OpenDamageWindow() {
            if (ctrl) {
                ctrl.OpenDamageWindow();
            }
        }

        public void CloseDamageWindow() {
            if (ctrl) {
                ctrl.CloseDamageWindow();
            }
        }
        
        public void SpawnProjectile() {
            if (ctrl) {
                ctrl.SpawnProjectile();                
            }
        }
        
        public void DestroyAndSpawnDebris() {
            if (ctrl) {
                ctrl.DestroyAndSpawnDebris();              
            }
        }
    }
}
