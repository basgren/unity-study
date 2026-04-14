using UnityEngine;

namespace Game.Features.Characters.Hero.GrapplingHook {
    /// <summary>
    /// Hook projectile that travels in a straight line toward a target anchor,
    /// then optionally retracts back to the hero. Uses kinematic Rigidbody2D
    /// with MovePosition for pixel-stable movement.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class GrapplingHookProjectile : MonoBehaviour {
        [SerializeField] private float travelSpeed = 25f;

        public bool HasArrived { get; private set; }
        public bool HasReturned { get; private set; }
        public Vector2 Position => rb.position;

        private Rigidbody2D rb;
        private Vector2 target;
        private Transform returnTarget;
        private bool isReturning;

        private void Awake() {
            rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        public void LaunchToward(Vector2 targetPos) {
            target = targetPos;
            HasArrived = false;
            HasReturned = false;
            isReturning = false;
        }

        public void ReturnTo(Transform hero) {
            returnTarget = hero;
            isReturning = true;
            HasReturned = false;
        }

        private void FixedUpdate() {
            if (HasReturned) {
                return;
            }

            if (isReturning) {
                MoveToward(returnTarget.position);
                if (Vector2.Distance(rb.position, returnTarget.position) < 0.1f) {
                    HasReturned = true;
                }
            } else if (!HasArrived) {
                MoveToward(target);
                if (Vector2.Distance(rb.position, target) < 0.1f) {
                    rb.MovePosition(target);
                    HasArrived = true;
                }
            }
        }

        private void MoveToward(Vector2 destination) {
            var pos = rb.position;
            var dir = destination - pos;

            // Orient the hook sprite along its travel direction (sprite assumed to face +X).
            if (dir.sqrMagnitude > 0.0001f) {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                rb.MoveRotation(angle);
            }

            var step = travelSpeed * Time.fixedDeltaTime;
            var newPos = Vector2.MoveTowards(pos, destination, step);
            rb.MovePosition(newPos);
        }
    }
}
