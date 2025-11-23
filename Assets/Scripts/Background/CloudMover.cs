using UnityEngine;

namespace Background {
    public class CloudMover : MonoBehaviour {
        [SerializeField] Transform backgroundObject;
        [SerializeField] [Range(-1f, 1f)] float moveSpeed = 0.001f;

        Renderer cloudRenderer;
        Bounds cloudBounds;

        private Renderer backgroundRenderer;
        float leftEdge;
        float rightEdge;

        private void Awake() {
            cloudRenderer = GetComponent<SpriteRenderer>();

            if (backgroundObject != null) {
                backgroundRenderer = backgroundObject.GetComponent<Renderer>();
            }

            if (backgroundRenderer == null) {
                Debug.LogError($"{name}: Background object has no Renderer.");
            }
        }

        private void Start() {
            var bgBounds = backgroundRenderer.bounds;
            leftEdge = bgBounds.min.x;
            rightEdge = bgBounds.max.x;
        }

        void Update() {
            transform.Translate(Vector3.right * (moveSpeed * Time.deltaTime));

            var bounds = cloudRenderer.bounds;
            var width = cloudBounds.size.x;

            if (moveSpeed > 0f && bounds.min.x > rightEdge) {
                transform.position = new Vector3(leftEdge - width, transform.position.y, transform.position.z);
            } else if (moveSpeed < 0f && bounds.max.x < leftEdge) {
                transform.position = new Vector3(rightEdge + width, transform.position.y, transform.position.z);
            }
        }
    }
}
