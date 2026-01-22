using Core.Components.Collisions;
using UnityEngine;

namespace Prefabs.Characters.Seashell {
    public class SeashellAI : MonoBehaviour {
        [SerializeField]
        private LayerCheck vision;

        [SerializeField]
        private LayerCheck attackTrigger;

        private SeashellController ctrl;

        private void Awake() {
            ctrl = GetComponent<SeashellController>();
        }

        private void Update() {
            if (attackTrigger.IsColliding() && ctrl.CanBite()) {
                ctrl.Bite();
                return;
            }
            
            if (vision.IsColliding() && ctrl.CanShoot()) {
                ctrl.Shoot();
            }
        }
    }
}
