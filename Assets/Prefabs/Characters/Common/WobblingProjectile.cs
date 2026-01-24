using UnityEngine;

namespace Prefabs.Characters.Common {
    [RequireComponent(typeof(Rigidbody2D))]
    public class WobblingProjectile : ProjectileBase {
        /// <summary>
        /// Projectile amplitude in blocks.
        /// </summary>
        [Header("Custom Settings")]
        [SerializeField]
        private float amplitude = 1f;
        
        /// <summary>
        /// Frequency in blocks.
        /// </summary>
        [SerializeField]
        private float frequency = 2f;
        
        protected override Vector2 GetNewPosition(Vector2 currentPos) {
            var invert = invertDirection ? -1 : 1;

            return new Vector2(
                currentPos.x + invert * linearSpeed * DeltaTime * DirectionScale,
                StartPosition.y + amplitude * Mathf.Sin(LifeTime * frequency * 2f * Mathf.PI)
            );
        }
    }
}
