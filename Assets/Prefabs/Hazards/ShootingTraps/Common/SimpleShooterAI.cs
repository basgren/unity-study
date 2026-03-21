using Core.Components.Collisions;
using UnityEngine;

namespace Prefabs.Hazards.ShootingTraps.Common {
    [RequireComponent(typeof(SimpleShooter))]
    public class SimpleShooterAI : MonoBehaviour {
        [SerializeField]
        private LayerCheck vision;

        private SimpleShooter ctrl;

        private void Awake() {
            ctrl = GetComponent<SimpleShooter>();
        }

        private void FixedUpdate() {
            if (vision.IsColliding()) {
                ctrl.Shoot();
            }
        }
    }
}
