using UnityEngine;

namespace Game.Core.Utils {
    public static class MathFn {
        private const float PI2 = Mathf.PI * 2;

        /// <summary>
        /// Takes a value between 0 and 1 and returns a value between 0 and 1 that follows a periodic pattern (sin).
        /// Phases are shifted, so Periodic(0) = 0, Periodic(0.5f) = 1 and Periodic(1f) = 0.
        /// Useful for creating smooth oscillating animations.
        /// </summary>
        /// <param name="progress"></param>
        /// <returns></returns>
        public static float Periodic(float progress) {
            return Mathf.Sin(progress * PI2 - 0.5f) * 0.5f + 0.5f;
        }
    }
}
