using UnityEngine;

namespace Game.Core.Services.SceneState.Savers {
    /// <summary>
    /// Saves and restores the transform position (and optionally rotation) of a GameObject.
    /// Intended for physics-driven props like draggable barrels — not for in-flight projectiles.
    /// On restore, Rigidbody2D velocity is zeroed to avoid physics overlaps.
    /// </summary>
    public sealed class TransformStateSaver : StateSaverBase {
        [SerializeField]
        private bool saveRotation;

        public override string Slot => "transform";

        public override void Capture(IStateWriter w) {
            w.SetVector2("p", transform.position);

            if (saveRotation) {
                w.SetFloat("r", transform.eulerAngles.z);
            }
        }

        public override void Restore(IStateReader r) {
            if (r.TryGetVector2("p", out var p)) {
                transform.position = p;

                // Clear velocity to avoid overlap forces when the barrel is restored mid-air.
                var rb = GetComponent<Rigidbody2D>();
                if (rb != null) {
                    rb.linearVelocity = Vector2.zero;
                }
            }

            if (saveRotation && r.TryGetFloat("r", out var rot)) {
                transform.rotation = Quaternion.Euler(0f, 0f, rot);
            }
        }
    }
}
