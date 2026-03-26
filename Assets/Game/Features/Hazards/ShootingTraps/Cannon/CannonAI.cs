using Core.Components.Collisions;
using UnityEngine;

namespace Game.Features.Hazards.ShootingTraps.Cannon {
    public class CannonAI : MonoBehaviour {
        [SerializeField]
        private LayerCheck vision;

        private CannonController ctrl;

        private void Awake() {
            ctrl = GetComponent<CannonController>();
        }

        private void FixedUpdate() {
            if (vision.IsColliding() && ctrl.CanShoot()) {
                ctrl.Shoot();
            }
        }
    }
}
