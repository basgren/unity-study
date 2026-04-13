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
        [SerializeField] private int constraintIterations = 5;
        [SerializeField] private float gravityScale = 20f;
        [SerializeField] private float damping = 0.98f;

        private Vector2[] currentPos;
        private Vector2[] previousPos;
        private Transform[] segments;
        private float segmentLength;
        private bool initialized;

        /// <summary>
        /// Creates the rope between two world-space points.
        /// </summary>
        public void Initialize(Vector2 anchorPoint, Vector2 heroPoint) {
            int pointCount = segmentCount + 1;
            currentPos = new Vector2[pointCount];
            previousPos = new Vector2[pointCount];
            segments = new Transform[segmentCount];

            // Distribute points evenly between anchor and hero
            for (int i = 0; i < pointCount; i++) {
                float t = (float)i / segmentCount;
                var pos = Vector2.Lerp(anchorPoint, heroPoint, t);
                currentPos[i] = pos;
                previousPos[i] = pos;
            }

            segmentLength = Vector2.Distance(anchorPoint, heroPoint) / segmentCount;

            // Instantiate segment sprites
            for (int i = 0; i < segmentCount; i++) {
                if (segmentPrefab != null) {
                    var go = Instantiate(segmentPrefab, currentPos[i], Quaternion.identity, transform);
                    segments[i] = go.transform;
                }
            }

            initialized = true;
        }

        /// <summary>
        /// Updates the pinned endpoints each frame. Point 0 is the anchor end,
        /// point N is the hero end.
        /// </summary>
        public void UpdateEndpoints(Vector2 anchorPoint, Vector2 heroPoint) {
            if (!initialized) {
                return;
            }

            currentPos[0] = anchorPoint;
            currentPos[currentPos.Length - 1] = heroPoint;

            // Recalculate rest length based on current endpoint distance
            float dist = Vector2.Distance(anchorPoint, heroPoint);
            segmentLength = dist / segmentCount;
        }

        /// <summary>
        /// Advances the Verlet simulation one fixed step. Call from FixedUpdate.
        /// </summary>
        public void Simulate(float fixedDelta) {
            if (!initialized) {
                return;
            }

            var gravity = new Vector2(0f, -gravityScale);

            // Verlet integration for non-pinned points
            for (int i = 1; i < currentPos.Length - 1; i++) {
                var cur = currentPos[i];
                var prev = previousPos[i];
                var velocity = (cur - prev) * damping;
                previousPos[i] = cur;
                currentPos[i] = cur + velocity + gravity * (fixedDelta * fixedDelta);
            }

            // Pin endpoints
            currentPos[0] = currentPos[0]; // anchor (already set by UpdateEndpoints)
            // hero endpoint is already set by UpdateEndpoints

            // Distance constraint solver
            for (int iter = 0; iter < constraintIterations; iter++) {
                for (int i = 0; i < currentPos.Length - 1; i++) {
                    var a = currentPos[i];
                    var b = currentPos[i + 1];
                    var delta = b - a;
                    float dist = delta.magnitude;

                    if (dist < 0.0001f) {
                        continue;
                    }

                    float error = (dist - segmentLength) / dist;
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
