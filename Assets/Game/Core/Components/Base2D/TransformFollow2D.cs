using UnityEngine;

namespace Game.Core.Components.Base2D {
    /// <summary>
    /// Locks this object's world position to a target Transform each LateUpdate.
    /// Use it when an object should "stick" to another object's position without
    /// becoming its Unity child (e.g. when child-of-flipping-parent would mirror
    /// the sprite, but we still want the position to follow).
    /// </summary>
    public class TransformFollow2D : MonoBehaviour {
        [SerializeField]
        private Transform target;

        public Transform Target {
            get => target;
            set => target = value;
        }

        private void LateUpdate() {
            if (target == null) {
                return;
            }

            Vector3 p = target.position;
            // Preserve our own Z so depth/sorting setup on the followed object stays untouched.
            transform.position = new Vector3(p.x, p.y, transform.position.z);
        }
    }
}
