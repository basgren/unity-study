using System;
using System.Collections.Generic;
using UnityEngine;

namespace Prefabs.Characters.Sharky {
    public enum PatrolPathDirection {
        Increasing,
        Decreasing
    }
    
    public class GroundPatrolPath : MonoBehaviour {
        [Header("Waypoints (World Space)")]
        [SerializeField, Min(0)]
        private int startIndex;

        [SerializeField]
        private PatrolPathDirection direction = PatrolPathDirection.Increasing;
        
        [SerializeField]
        private List<PatrolPoint> points = new List<PatrolPoint>();

        public int StartIndex => startIndex;
        public IList<PatrolPoint> Points => points;

        private int currentTargetIndex;
        private PatrolPathDirection currentDirection;

        public int CurrentTargetIndex => currentTargetIndex;

        private void Awake() {
            ResetPoint();
        }

        public PatrolPoint GetTargetPoint() {
            return points[currentTargetIndex];
        }

        public void NextTarget() {
            if (points.Count < 2) {
                return;
            }
            
            var increment = GetIncrement();
            var nextIndex = currentTargetIndex + increment;

            if (nextIndex >= points.Count || nextIndex < 0) {
                currentDirection = currentDirection == PatrolPathDirection.Increasing
                    ? PatrolPathDirection.Decreasing
                    : PatrolPathDirection.Increasing;
                
                NextTarget();
                return;
            }
            
            currentTargetIndex = nextIndex;
        }

        private int GetIncrement() {
            return currentDirection == PatrolPathDirection.Increasing ? 1 : -1;
        }
        
        private void ResetPoint() {
            currentTargetIndex = startIndex;
            currentDirection = direction;
        }

#if UNITY_EDITOR
        private void OnValidate() {
            if (points == null || points.Count == 0) {
                startIndex = 0;
                return;
            }

            startIndex = Mathf.Clamp(startIndex, 0, points.Count - 1);
        }

        private void OnDrawGizmosSelected() {
            var defaultColor = Gizmos.color;

            for (var i = 0; i < points.Count; i++) {
                var p = points[i].position;

                Gizmos.color = i == startIndex ? Color.cyan : defaultColor;
                Gizmos.DrawSphere(p, 0.08f);

                var next = i + 1;
                if (next < points.Count) {
                    Gizmos.color = defaultColor;
                    Gizmos.DrawLine(p, points[next].position);
                }

                // Draw the index label slightly below the point to reduce overlap.
                UnityEditor.Handles.Label(p + Vector2.down * 0.18f, i.ToString());
            }

            Gizmos.color = defaultColor;
        }
#endif
    }

    [Serializable]
    public class PatrolPoint {
        public Vector2 position; // World-space
        
        /// <summary>
        /// Delay associated with point. For example, character can make a delay before moving to another point.
        /// </summary>
        public float delay;

        public PatrolPoint(Vector2 position, float delay = 0f) {
            this.position = position;
            this.delay = delay;
        }
    }
}
