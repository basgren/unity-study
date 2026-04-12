using System.Collections;
using UnityEngine;

namespace Core.Components.Effects {
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

        [SerializeField]
        private float minLifeTime = 8f;

        [SerializeField]
        private float maxLifeTime = 13f;

        [SerializeField]
        private float fadeOutDuration = 1f;

        private DebrisData[] debrisData;

        private void Start() {
            var rigidBodies = GetComponentsInChildren<Rigidbody2D>();
            debrisData = new DebrisData[rigidBodies.Length];

            for (var i = 0; i < rigidBodies.Length; i++) {
                var rb = rigidBodies[i];
                var sr = rb.gameObject.GetComponent<SpriteRenderer>();

                debrisData[i] = new DebrisData {
                    rigidbody = rb,
                    spriteRenderer = sr,
                    initialPosition = rb.transform.localPosition,
                    initialRotation = rb.transform.localRotation,
                    destroyTime = Time.time + Random.Range(minLifeTime, maxLifeTime),
                };
            }

            PlayAnimation();
        }

        private void FixedUpdate() {
            if (gameObject == null) {
                return;
            }
            
            var destroyedCount = 0;

            for (var i = 0; i < debrisData.Length; i++) {
                var data = debrisData[i];
                var isDestroyed = data.rigidbody == null;

                if (isDestroyed) {
                    destroyedCount++;
                } else {
                    if (!data.isDestroying && data.destroyTime < Time.time) {
                        StartCoroutine(StartFadeOut(data, fadeOutDuration));

                        debrisData[i].isDestroying = true; // override struct value
                    }
                }
            }

            if (destroyedCount == debrisData.Length) {
                Debug.Log("Destroying full object");
                Destroy(gameObject);
            }
        }

        private IEnumerator StartFadeOut(DebrisData debris, float duration) {
            while (true) {
                var sr = debris.spriteRenderer;

                if (sr == null) {
                    yield break;
                }
                
                var progress = Mathf.Clamp01((Time.time - debris.destroyTime) / duration);
                if (Mathf.Approximately(progress, 1f)) {
                    Destroy(sr.gameObject);
                } else {
                    var color = sr.color;
                    color.a = 1f - progress;
                    sr.color = color;
                }
                
                yield return null;
            }
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
            public SpriteRenderer spriteRenderer;
            public Vector3 initialPosition;
            public Quaternion initialRotation;
            public float destroyTime;
            public bool isDestroying;
        }
    }
}
