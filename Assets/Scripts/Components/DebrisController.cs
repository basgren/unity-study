using UnityEngine;

namespace Components {
    // TODO: [BG]  Make debris disappear with time, but take "Replay Animation" menu item into account
    // TODO: [BG] Maybe we should freeze z rotation and rotate debris randomly by 90 degrees steps?
    
    public class DebrisController : MonoBehaviour {
        [SerializeField]
        private float minSpeed;

        [SerializeField]
        private float maxSpeed;

        [SerializeField]
        private float directionDeviation;

        [SerializeField]
        private float minRotation;

        [SerializeField]
        private float maxRotation;

        private DebrisData[] debrisData;

        private void Start() {
            var rigidBodies = GetComponentsInChildren<Rigidbody2D>();
            debrisData = new DebrisData[rigidBodies.Length];

            for (var i = 0; i < rigidBodies.Length; i++) {
                var rb = rigidBodies[i];

                debrisData[i] = new DebrisData {
                    rigidbody = rb,
                    initialPosition = rb.transform.localPosition,
                    initialRotation = rb.transform.localRotation
                };
            }

            PlayAnimation();
        }

        [ContextMenu("Replay Animation")]
        public void PlayAnimation() {
            if (debrisData == null) return;

            var position = transform.position;

            foreach (var data in debrisData) {
                var rb = data.rigidbody;
                if (rb == null || rb.gameObject == gameObject) {
                    continue;
                }

                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0;
                rb.transform.localPosition = data.initialPosition;
                rb.transform.localRotation = data.initialRotation;

                var direction = (rb.transform.position - position).normalized;
                var angle = Random.Range(-directionDeviation, directionDeviation);
                var rotation = Quaternion.AngleAxis(angle, Vector3.forward);
                var finalDirection = rotation * direction;

                var speed = Random.Range(minSpeed, maxSpeed);
                rb.velocity = finalDirection * speed;

                var angularVelocity = Random.Range(minRotation, maxRotation);
                rb.angularVelocity = angularVelocity;
            }
        }

        private struct DebrisData {
            public Rigidbody2D rigidbody;
            public Vector3 initialPosition;
            public Quaternion initialRotation;
        }
    }
}
