using UnityEngine;

namespace Game.Core.Utils {
    /// <summary>
    /// Helpers for drawing editor gizmos the built-in <see cref="Gizmos"/> API can't draw directly
    /// (thick, curved lines). Editor-only rendering paths are guarded with <c>UNITY_EDITOR</c>, so the
    /// methods are safe to call from a runtime <c>OnDrawGizmos</c> / <c>OnDrawGizmosSelected</c>.
    /// </summary>
    public static class GizmoUtils {
        /// <summary>
        /// Draws a circular-arc polyline between <paramref name="from"/> and <paramref name="to"/> in
        /// the XY plane, bowing toward the left of the from→to direction. <paramref name="radius"/> sets
        /// the curvature (larger = flatter); it is clamped to at least half the chord (a semicircle).
        /// Uses anti-aliased thick <c>Handles</c> lines in the editor and falls back to plain
        /// <c>Gizmos</c> segments in a build.
        /// </summary>
        public static void DrawArc(Vector3 from, Vector3 to, float radius, Color color, float width, int segments) {
            Vector3[] points = BuildArc(from, to, radius, segments);
#if UNITY_EDITOR
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.DrawAAPolyLine(width, points);
#else
            Gizmos.color = color;
            for (int i = 0; i < points.Length - 1; i++) {
                Gizmos.DrawLine(points[i], points[i + 1]);
            }
#endif
        }

        /// <summary>
        /// Samples a circular arc between two points in the XY plane as a polyline of
        /// <paramref name="segments"/> segments. <paramref name="radius"/> is clamped to at least half
        /// the chord; the arc bows toward the left normal of the from→to direction. Z is interpolated
        /// between the endpoints.
        /// </summary>
        public static Vector3[] BuildArc(Vector3 from, Vector3 to, float radius, int segments) {
            segments = Mathf.Max(1, segments);
            var points = new Vector3[segments + 1];

            Vector3 chord = to - from;
            float chordLength = chord.magnitude;
            if (chordLength < 0.0001f) {
                for (int i = 0; i <= segments; i++) {
                    points[i] = from;
                }

                return points;
            }

            float half = chordLength * 0.5f;
            float r = Mathf.Max(radius, half); // The radius can't be smaller than half the chord.

            Vector3 dir = chord / chordLength;
            Vector3 normal = new Vector3(-dir.y, dir.x, 0f); // Left normal in the XY plane.
            Vector3 mid = (from + to) * 0.5f;
            // Place the centre opposite the normal so the arc bulges toward it (sagitta = r - centreDist).
            float centerDist = Mathf.Sqrt(r * r - half * half);
            Vector3 center = mid - normal * centerDist;

            float startAngle = Mathf.Atan2(from.y - center.y, from.x - center.x);
            float endAngle = Mathf.Atan2(to.y - center.y, to.x - center.x);
            // Sweep the short way around the circle.
            float delta = Mathf.DeltaAngle(startAngle * Mathf.Rad2Deg, endAngle * Mathf.Rad2Deg) * Mathf.Deg2Rad;

            for (int i = 0; i <= segments; i++) {
                float t = (float)i / segments;
                float a = startAngle + delta * t;
                points[i] = new Vector3(
                    center.x + Mathf.Cos(a) * r,
                    center.y + Mathf.Sin(a) * r,
                    Mathf.Lerp(from.z, to.z, t));
            }

            return points;
        }
    }
}
