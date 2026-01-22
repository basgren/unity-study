using UnityEngine;

namespace Prefabs.Characters.Common {
    [RequireComponent(typeof(Rigidbody2D))]
    public class LinearProjectile : ProjectileBase {
        protected override Vector2 GetNewPosition(Vector2 currentPos) {
            var invert = invertDirection ? -1 : 1;
            return currentPos + new Vector2(invert * linearSpeed * DeltaTime * DirectionScale, 0);
        }
    }
}
