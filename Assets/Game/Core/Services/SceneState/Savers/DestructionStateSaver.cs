using Game.Core.Bootstrap;
using UnityEngine;

namespace Game.Core.Services.SceneState.Savers {
    /// <summary>
    /// Saves whether a scene-placed object has been permanently destroyed.
    /// Default state (object present in scene) = not destroyed.
    /// The saver can only record destruction — it cannot resurrect objects.
    ///
    /// Callers must use MarkDestroyedAndDestroy() instead of Destroy() so the state is pushed
    /// into the service immediately, even before the next scene unload.
    /// </summary>
    public sealed class DestructionStateSaver : StateSaverBase {
        private bool destroyed;
        private StateRoot stateRoot;

        public override string Slot => "destroyed";

        private void Awake() {
            stateRoot = GetComponent<StateRoot>();
        }

        /// <summary>
        /// Call this instead of Destroy() when the object is permanently removed.
        /// Pushes the destruction state into SceneStateService immediately, then destroys the GameObject.
        /// </summary>
        public void MarkDestroyedAndDestroy() {
            MarkDestroyed();
            Destroy(gameObject);
        }

        /// <summary>
        /// Call this instead of Destroy() when the object is permanently removed.
        /// Pushes the destruction state into SceneStateService immediately.
        /// </summary>
        public void MarkDestroyed() {
            if (destroyed) {
                return;
            }

            destroyed = true;

            // Push immediately so state is captured even if the player force-quits before leaving the scene.
            if (stateRoot != null && G.SceneState != null) {
                G.SceneState.PushSlot(stateRoot, Slot, w => w.SetBool("d", true));
            }
        }

        public override void Capture(IStateWriter w) {
            w.SetBool("d", destroyed);
        }

        public override void Restore(IStateReader r) {
            if (r.TryGetBool("d", out var d) && d) {
                // The scene has re-instantiated us; destroy immediately to match the saved state.
                Destroy(gameObject);
            }
        }
    }
}
