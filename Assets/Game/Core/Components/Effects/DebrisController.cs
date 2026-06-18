using System.Collections;
using UnityEngine;

namespace Game.Core.Components.Effects {
    // TODO: [BG] Maybe we should freeze z rotation and rotate debris randomly by 90 degrees steps?

    public enum DebrisSpawnMode {
        // Use existing child objects as debris pieces at their authored positions.
        Static,

        // Use existing children as templates; spawn random instances within a rectangular area.
        RandomArea,
    }

    public class DebrisController : MonoBehaviour {
        [SerializeField]
        private DebrisSpawnMode spawnMode = DebrisSpawnMode.Static;

        [Header("Random Area Mode")]
        [SerializeField]
        private int spawnCount = 10;

        [SerializeField]
        private Vector2 spawnAreaCenter = Vector2.zero;

        [SerializeField]
        private Vector2 spawnAreaSize = Vector2.one;

        [Header("Physics")]
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
            if (spawnMode == DebrisSpawnMode.RandomArea) {
                SpawnRandomDebris();
            }

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

        private void SpawnRandomDebris() {
            var childCount = transform.childCount;
            if (childCount == 0 || spawnCount <= 0) {
                return;
            }

            // Snapshot templates first so freshly spawned children are not used as templates themselves.
            var templates = new GameObject[childCount];
            for (var i = 0; i < childCount; i++) {
                templates[i] = transform.GetChild(i).gameObject;
            }

            var halfWidth = spawnAreaSize.x * 0.5f;
            var halfHeight = spawnAreaSize.y * 0.5f;

            for (var i = 0; i < spawnCount; i++) {
                var template = templates[Random.Range(0, templates.Length)];
                var spawned = Instantiate(template, transform);
                spawned.transform.localPosition = new Vector3(
                    spawnAreaCenter.x + Random.Range(-halfWidth, halfWidth),
                    spawnAreaCenter.y + Random.Range(-halfHeight, halfHeight),
                    0f
                );
            }

            // Hide templates so they are not picked up by the debris physics pass.
            foreach (var template in templates) {
                template.SetActive(false);
            }
        }

        private void OnDrawGizmosSelected() {
            if (spawnMode != DebrisSpawnMode.RandomArea) {
                return;
            }

            Gizmos.color = Color.cyan;
            var center = transform.position + (Vector3)spawnAreaCenter;
            var size = new Vector3(spawnAreaSize.x, spawnAreaSize.y, 0f);
            Gizmos.DrawWireCube(center, size);
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

                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0;
                rb.transform.localPosition = data.initialPosition;
                rb.transform.localRotation = data.initialRotation;

                var direction = (rb.transform.position - position).normalized;
                var angle = Random.Range(-directionDeviation, directionDeviation);
                var rotation = Quaternion.AngleAxis(angle, Vector3.forward);
                var finalDirection = rotation * direction;

                var speed = Random.Range(minSpeed, maxSpeed);
                rb.linearVelocity = finalDirection * speed;

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
