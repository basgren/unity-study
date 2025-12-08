using UnityEngine;

namespace Utils {
    /// <summary>
    /// Tracks the player's last stable ground position. The point is saved only
    /// after the player has been grounded for a minimum stable time, ensuring that
    /// respawns never occur on the very edge of a platform.
    /// 
    /// No MonoBehaviour dependency.
    /// Call Update() every frame after ground check.
    /// </summary>
    public class SafePointTracker {
        private readonly float minStableGroundTime; // How long player must stand on ground
        private readonly float backoffDistance; // Push back slightly from the edge

        public Vector2 LastSafePosition { get; private set; }
        public bool HasSafePosition { get; private set; }

        private float groundedStableTime;

        public SafePointTracker(
            float minStableGroundTime = 0.1f
        ) {
            this.minStableGroundTime = minStableGroundTime;
            HasSafePosition = false;
        }

        /// <summary>
        /// Updates the safe point logic.
        /// Should be called AFTER ground check.
        /// </summary>
        /// <param name="isGrounded">Whether character is on the ground this frame.</param>
        /// <param name="position">Current world position of the character.</param>
        /// <param name="velocity">Current velocity of the character.</param>
        /// <param name="deltaTime">Time.deltaTime from caller.</param>
        public void Update(
            bool isGrounded,
            Vector2 position,
            Vector2 velocity,
            float deltaTime
        ) {
            if (isGrounded && velocity.y <= 0.01f) { 
                groundedStableTime += deltaTime;

                if (groundedStableTime >= minStableGroundTime) {
                    float moveDir = 0f;

                    Vector2 safePos = position;

                    LastSafePosition = safePos;
                    HasSafePosition = true;
                    groundedStableTime = 0f;
                }
            } else {
                groundedStableTime = 0f;
            }
        }

        /// <summary>
        /// Clears previously saved safe point.
        /// </summary>
        public void Reset() {
            HasSafePosition = false;
            groundedStableTime = 0f;
        }
    }
}
