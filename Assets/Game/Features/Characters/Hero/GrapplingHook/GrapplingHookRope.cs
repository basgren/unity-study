using UnityEngine;

namespace Game.Features.Characters.Hero.GrapplingHook {
    /// <summary>
    /// Visual rope using Verlet integration. Simulates N points between two
    /// pinned endpoints, renders a sprite at each segment. The rope is purely
    /// visual and does not affect physics.
    /// </summary>
    public class GrapplingHookRope : MonoBehaviour {
        [SerializeField] private GameObject segmentPrefab;
        [SerializeField] private int segmentCount = 10;
        [SerializeField] private int constraintIterations = 8;
        [SerializeField] private float gravityScale = 20f;
        [SerializeField] private float damping = 0.9f;

        private Vector2[] currentPos;
        private Vector2[] previousPos;
        private Transform[] segments;
        private float segmentLength;
        private bool initialized;

        /// <summary>
        /// Creates the rope with all points collapsed at a single starting position.
        /// The rope will extend naturally as <see cref="UpdateEndpoints"/> moves the
        /// anchor endpoint away.
        /// </summary>
        public void Initialize(Vector2 startPosition) {
            int pointCount = segmentCount + 1;
            currentPos = new Vector2[pointCount];
            previousPos = new Vector2[pointCount];
            segments = new Transform[segmentCount];

            for (int i = 0; i < pointCount; i++) {
                currentPos[i] = startPosition;
                previousPos[i] = startPosition;
            }

            segmentLength = 0f;

            for (int i = 0; i < segmentCount; i++) {
                if (segmentPrefab != null) {
                    var go = Instantiate(segmentPrefab, startPosition, Quaternion.identity, transform);
                    segments[i] = go.transform;
                }
            }

            initialized = true;
        }

        /// <summary>
        /// Updates the pinned endpoints each frame. Point 0 is the anchor/hook end,
        /// point N is the hero end.
        /// </summary>
        public void UpdateEndpoints(Vector2 anchorPoint, Vector2 heroPoint) {
            if (!initialized) {
                return;
            }

            currentPos[0] = anchorPoint;
            currentPos[currentPos.Length - 1] = heroPoint;
        }

        /// <summary>
        /// Locks the rope rest length to the current endpoint distance.
        /// Call once when the hook attaches to the anchor.
        /// </summary>
        public void LockLength(float totalLength) {
            segmentLength = totalLength / segmentCount;
        }

        /// <summary>
        /// Advances the Verlet simulation one fixed step. Call from FixedUpdate.
        /// </summary>
        public void Simulate(float fixedDelta) {
            if (!initialized) {
                return;
            }

            // While segment length is not locked, derive it from current endpoint distance
            // (during shooting/retracting the rope stretches with the projectile)
            float currentDist = Vector2.Distance(currentPos[0], currentPos[currentPos.Length - 1]);
            float activeSegmentLength = segmentLength > 0f
                ? segmentLength
                : currentDist / segmentCount;

            var gravity = new Vector2(0f, -gravityScale);

            // Verlet integration for non-pinned points
            for (int i = 1; i < currentPos.Length - 1; i++) {
                var cur = currentPos[i];
                var prev = previousPos[i];
                var velocity = (cur - prev) * damping;
                previousPos[i] = cur;
                currentPos[i] = cur + velocity + gravity * (fixedDelta * fixedDelta);
            }

            // Distance constraint solver
            for (int iter = 0; iter < constraintIterations; iter++) {
                // Pin endpoints
                currentPos[0] = currentPos[0];
                currentPos[currentPos.Length - 1] = currentPos[currentPos.Length - 1];

                for (int i = 0; i < currentPos.Length - 1; i++) {
                    var a = currentPos[i];
                    var b = currentPos[i + 1];
                    var delta = b - a;
                    float dist = delta.magnitude;

                    if (dist < 0.0001f) {
                        continue;
                    }

                    float error = (dist - activeSegmentLength) / dist;
                    var correction = delta * (0.5f * error);

                    // Pin first and last points
                    if (i == 0) {
                        currentPos[i + 1] -= correction * 2f;
                    } else if (i + 1 == currentPos.Length - 1) {
                        currentPos[i] += correction * 2f;
                    } else {
                        currentPos[i] += correction;
                        currentPos[i + 1] -= correction;
                    }
                }
            }
        }

        /// <summary>
        /// Positions and rotates segment sprites along the rope. Call from LateUpdate.
        /// </summary>
        public void Render() {
            if (!initialized) {
                return;
            }

            for (int i = 0; i < segmentCount; i++) {
                if (segments[i] == null) {
                    continue;
                }

                var pos = currentPos[i];
                var next = currentPos[i + 1];
                segments[i].position = (Vector3)pos;

                var dir = next - pos;
                if (dir.sqrMagnitude > 0.0001f) {
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    segments[i].rotation = Quaternion.Euler(0f, 0f, angle);
                }
            }
        }

        /// <summary>
        /// Destroys all segment GameObjects and the rope itself.
        /// </summary>
        public void Cleanup() {
            if (segments != null) {
                for (int i = 0; i < segments.Length; i++) {
                    if (segments[i] != null) {
                        Destroy(segments[i].gameObject);
                    }
                }
            }

            Destroy(gameObject);
        }
    }
}
