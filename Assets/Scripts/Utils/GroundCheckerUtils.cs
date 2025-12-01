using UnityEngine;

namespace Utils {
    public static class GroundCheckerUtils {
        public static void DrawGroundCheckerGizmos(GroundChecker groundChecker) {
            if (groundChecker == null) {
                return;
            }

            for (var i = 0; i < groundChecker.RayCount; i++) {
                Gizmos.color = groundChecker.HasRayCollision(i)
                    ? Color.green
                    : Color.red;

                Vector2 rayOrigin = groundChecker.RayOrigins[i];
                Gizmos.DrawLine(rayOrigin, rayOrigin + groundChecker.RayLength * groundChecker.RayDirection);
            }
        }
    }
}
