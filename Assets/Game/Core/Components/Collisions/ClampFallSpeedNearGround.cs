using UnityEngine;

namespace Core.Components.Collisions {
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class ClampFallSpeedNearGround : MonoBehaviour {
        [SerializeField]
        private LayerMask groundMask;

        [SerializeField]
        private float checkDistance = 1f;

        [SerializeField]
        private float normalMaxFallSpeed = -12f;

        [SerializeField]
        private float nearGroundMaxFallSpeed = -2f;

        private Rigidbody2D rb;
        private Collider2D circle;

        private void Awake() {
            rb = GetComponent<Rigidbody2D>();
            circle = GetComponent<Collider2D>();
        }

        private void FixedUpdate() {
            float y = rb.linearVelocity.y;
            if (y >= 0f) {
                return;
            }

            Vector2 origin = circle.bounds.center;
            float radius = circle.bounds.extents.x * 0.95f;

            RaycastHit2D hit = Physics2D.CircleCast(origin, radius, Vector2.down, checkDistance, groundMask);

            float maxFallSpeed = normalMaxFallSpeed;

            if (hit.collider != null) {
                float t = 1f - (hit.distance / checkDistance);
                maxFallSpeed = Mathf.Lerp(normalMaxFallSpeed, nearGroundMaxFallSpeed, t);
            }

            if (y < maxFallSpeed) {
                y = maxFallSpeed;
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, y);
            }
        }
    }
}
