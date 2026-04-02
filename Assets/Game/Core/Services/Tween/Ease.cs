using UnityEngine;

namespace Game.Core.Services.Tween {
    public enum EaseType {
        Linear,
        InQuad,
        OutQuad,
        InOutQuad,
        InCubic,
        OutCubic,
        InOutCubic
    }

    /// <summary>
    /// Static easing formulas. All methods are pure math with no Unity API calls.
    /// </summary>
    public static class Ease {
        public static float Evaluate(EaseType type, float t) {
            t = Mathf.Clamp01(t);
            return type switch {
                EaseType.Linear => t,
                EaseType.InQuad => t * t,
                EaseType.OutQuad => t * (2f - t),
                EaseType.InOutQuad => InOutQuad(t),
                EaseType.InCubic => t * t * t,
                EaseType.OutCubic => OutCubic(t),
                EaseType.InOutCubic => InOutCubic(t),
                _ => t
            };
        }

        private static float InOutQuad(float t) {
            return t < 0.5f
                ? 2f * t * t
                : -1f + (4f - 2f * t) * t;
        }
        
        private static float OutCubic(float t) {
            float m = t - 1f;
            return 1f + m * m * m;
        }

        private static float InOutCubic(float t) {
            if (t < 0.5f) {
                return 4f * t * t * t;
            }

            float m = 2f * t - 2f;
            return 1f + m * m * m / 2f;
        }
    }
}
