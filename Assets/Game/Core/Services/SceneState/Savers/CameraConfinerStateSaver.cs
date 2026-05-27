using Game.Core.Components.Camera;
using UnityEngine;

// This saver pairs with the deprecated SwapCameraConfinerShape and is kept for the scenes that still
// use it. New work should use CameraConfinerSelector (and a selector-based saver). The obsolete
// warning is suppressed here because supporting the deprecated component is this file's whole purpose.
#pragma warning disable CS0618
namespace Game.Core.Services.SceneState.Savers {
    /// <summary>
    /// Saves whether a <see cref="SwapCameraConfinerShape"/> has already widened the camera bounds.
    /// On restore, when the saved flag is set, re-applies the expanded shape immediately so a
    /// returning player sees the widened area without re-triggering the original event (e.g. the
    /// boss does not have to be killed again). State is captured on scene leave (snapshot-on-leave).
    /// </summary>
    public sealed class CameraConfinerStateSaver : StateSaverBase {
        private SwapCameraConfinerShape swap;

        public override string Slot => "confiner";

        private void Awake() {
            swap = GetComponent<SwapCameraConfinerShape>();
        }

        public override void Capture(IStateWriter w) {
            w.SetBool("expanded", swap.IsExpanded);
        }

        public override void Restore(IStateReader r) {
            if (r.TryGetBool("expanded", out var expanded) && expanded) {
                swap.ApplyExpandedImmediate();
            }
        }
    }
}
#pragma warning restore CS0618
