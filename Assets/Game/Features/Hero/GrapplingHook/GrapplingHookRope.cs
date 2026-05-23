using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Features.Characters.Hero.GrapplingHook {
    /// <summary>
    /// Visual rope rendered as a straight chain of fixed-length segments between two
    /// pinned endpoints. Purely visual — does not affect physics.
    ///
    /// The rope assumes the player is holding it taut (which is what the
    /// <c>DistanceJoint2D</c> on the hero enforces while attached), so there is no
    /// Verlet simulation, gravity sag, or constraint solver — every segment is exactly
    /// <see cref="segmentRestLength"/>, distributed along the straight line between
    /// the endpoints. The visible segment count is derived from the current rope length
    /// so density stays roughly constant whether the player is at the end of a long
    /// rope or has climbed up close to the anchor. Segments beyond the current active
    /// count are pooled via SetActive(false) and reused.
    ///
    /// Because every segment is exactly the rest length, the chain's far end falls up
    /// to one rest length short of the actual target endpoint. That "gap" is hidden by
    /// the sprite at whichever end is the moving one: during Shooting the rope grows
    /// from the hero hand (hook projectile sprite hides the gap); during Attached it
    /// grows from the anchor (hero hand sprite hides the gap).
    /// </summary>
    public class GrapplingHookRope : MonoBehaviour {
        [SerializeField] private GameObject segmentPrefab;

        [SerializeField, FormerlySerializedAs("segmentCount"),
         Tooltip("Pool size: maximum number of rope segments instantiated up front. Should be at least ceil(maxRopeLength / segmentRestLength).")]
        private int maxSegmentCount = 20;

        [SerializeField, Tooltip("World length of one rope segment in metres. Should match the sprite's world-space length so segments visually butt up against each other without gaps or overlap. Visible segment count = floor(ropeLength / segmentRestLength).")]
        private float segmentRestLength = 0.4f;

        private Transform[] segments;
        private Vector2 anchorPoint;
        private Vector2 heroPoint;
        // Total rope length captured by the latest LockLength() call. 0 while the rope
        // is still stretching with the projectile (Shooting state).
        private float lockedRopeLength;
        private int activeCount;
        private bool initialized;

        /// <summary>
        /// Pre-instantiates the full segment pool. No further Instantiate/Destroy happens
        /// at runtime — length changes only toggle SetActive on existing segments.
        /// </summary>
        /// <param name="startPosition">Initial world position; all segments collapse here.</param>
        /// <param name="maxRopeLength">Caller's max rope length, used only to warn when the
        /// pool is too small to render a fully-extended rope.</param>
        public void Initialize(Vector2 startPosition, float maxRopeLength) {
            if (segmentPrefab == null) {
                Debug.LogError($"{nameof(GrapplingHookRope)} on '{name}' has no segmentPrefab assigned.", this);
                return;
            }

            // Visible segment count = floor(ropeLength / segmentRestLength). If the pool can't
            // cover maxRopeLength, the chain visually falls short of the anchor when fully extended.
            if (segmentRestLength > 0f) {
                int requiredSegments = Mathf.CeilToInt(maxRopeLength / segmentRestLength);
                if (maxSegmentCount < requiredSegments) {
                    Debug.LogWarning(
                        $"{nameof(GrapplingHookRope)} on '{name}': maxSegmentCount={maxSegmentCount} is too small for " +
                        $"maxRopeLength={maxRopeLength} at segmentRestLength={segmentRestLength}. " +
                        $"Increase maxSegmentCount to at least {requiredSegments}.",
                        this
                    );
                }
            }

            segments = new Transform[maxSegmentCount];
            anchorPoint = startPosition;
            heroPoint = startPosition;

            for (int i = 0; i < maxSegmentCount; i++) {
                var go = Instantiate(segmentPrefab, startPosition, Quaternion.identity, transform);
                go.SetActive(false);
                segments[i] = go.transform;
            }

            lockedRopeLength = 0f;
            activeCount = 0;
            initialized = true;
        }

        /// <summary>
        /// Updates the rope endpoints each frame. <paramref name="anchor"/> is the hook /
        /// world anchor end, <paramref name="hero"/> is the hero grab end.
        /// </summary>
        public void UpdateEndpoints(Vector2 anchor, Vector2 hero) {
            if (!initialized) {
                return;
            }

            anchorPoint = anchor;
            heroPoint = hero;
        }

        /// <summary>
        /// Sets the rope's total rest length. The visible segment count is derived from
        /// this and <see cref="segmentRestLength"/>, so changing the locked length as the
        /// player climbs naturally adds/removes segments at the moving end.
        /// </summary>
        public void LockLength(float totalLength) {
            lockedRopeLength = totalLength;
        }

        /// <summary>
        /// Lays out the active segments along the straight line between the endpoints.
        /// Call from LateUpdate.
        /// </summary>
        public void Render() {
            if (!initialized) {
                return;
            }

            float liveDist = Vector2.Distance(anchorPoint, heroPoint);

            // Prefer the locked length so climbing changes count even when the hero
            // hasn't moved yet. During Shooting (no lock), the live endpoint distance
            // grows as the hook flies out.
            float ropeLength = lockedRopeLength > 0f ? lockedRopeLength : liveDist;

            int desired = segmentRestLength > 0f
                ? Mathf.Clamp(Mathf.FloorToInt(ropeLength / segmentRestLength), 1, maxSegmentCount)
                : maxSegmentCount;

            if (desired != activeCount) {
                SetActiveCount(desired);
            }

            // Degenerate case: endpoints coincide (just after Initialize, before the hook
            // has flown anywhere). Avoid divide-by-near-zero on the direction normalization.
            if (liveDist < 0.0001f) {
                for (int i = 0; i < activeCount; i++) {
                    segments[i].position = anchorPoint;
                }
                return;
            }

            // Choose growth origin:
            // - Attached (locked): grow from the world anchor end. The unfilled gap at
            //   the chain's far end sits at the hero hand, hidden by the hero sprite.
            // - Shooting (not locked): grow from the hero hand. The gap sits at the
            //   hook end, hidden by the hook projectile sprite.
            bool growFromAnchor = lockedRopeLength > 0f;
            var origin = growFromAnchor ? anchorPoint : heroPoint;
            var target = growFromAnchor ? heroPoint : anchorPoint;

            var direction = (target - origin) / liveDist;
            var rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

            for (int i = 0; i < activeCount; i++) {
                segments[i].position = origin + direction * (i * segmentRestLength);
                segments[i].rotation = rotation;
            }
        }

        private void SetActiveCount(int desired) {
            if (desired > activeCount) {
                for (int i = activeCount; i < desired; i++) {
                    segments[i].gameObject.SetActive(true);
                }
            } else {
                for (int i = desired; i < activeCount; i++) {
                    segments[i].gameObject.SetActive(false);
                }
            }

            activeCount = desired;
        }

        /// <summary>
        /// Destroys all segment GameObjects and the rope itself.
        /// </summary>
        public void Cleanup() {
            if (segments != null) {
                for (int i = 0; i < segments.Length; i++) {
                    Destroy(segments[i].gameObject);
                }
            }

            Destroy(gameObject);
        }
    }
}
