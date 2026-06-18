using System.Collections;
using Game.Core.Components.Animation;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Features.Props.HintBottle {
    /// <summary>
    /// Periodically plays a sparkle at a random point inside a collider zone.
    /// The collider only defines the spawn area, so it is captured once and disabled at start.
    /// </summary>
    [RequireComponent(typeof(SimpleSpriteAnimator))]
    public class HintBottleSpark : MonoBehaviour {
        [SerializeField]
        private BoxCollider2D zone;

        [SerializeField]
        private float minDelay = 4f;

        [SerializeField]
        private float maxDelay = 10f;

        private SimpleSpriteAnimator animator;

        // Spawn area captured in parent-local space so it survives moving this transform around.
        private Vector3 zoneCenterLocal;
        private Vector2 zoneHalfSize;

        void Awake() {
            animator = GetComponent<SimpleSpriteAnimator>();

            if (zone == null) {
                zone = GetComponent<BoxCollider2D>();
            }
        }

        void Start() {
            CaptureZone();
            StartCoroutine(SparkRoutine());
        }

        private void CaptureZone() {
            if (zone == null) {
                return;
            }

            Vector3 scale = transform.localScale;
            zoneCenterLocal = transform.localPosition
                + new Vector3(zone.offset.x * scale.x, zone.offset.y * scale.y, 0f);
            zoneHalfSize = new Vector2(zone.size.x * scale.x, zone.size.y * scale.y) * 0.5f;

            // The collider is only used to define the spawn area; it is no longer needed at runtime.
            zone.enabled = false;
        }

        private IEnumerator SparkRoutine() {
            while (true) {
                float delay = Random.Range(minDelay, maxDelay);
                yield return new WaitForSeconds(delay);

                PositionRandomly();

                // The animator disables itself after a non-looped run; re-enable before replaying.
                animator.enabled = true;
                animator.Play();
            }
        }

        private void PositionRandomly() {
            float x = zoneCenterLocal.x + Random.Range(-zoneHalfSize.x, zoneHalfSize.x);
            float y = zoneCenterLocal.y + Random.Range(-zoneHalfSize.y, zoneHalfSize.y);

            transform.localPosition = new Vector3(x, y, transform.localPosition.z);
        }
    }
}
