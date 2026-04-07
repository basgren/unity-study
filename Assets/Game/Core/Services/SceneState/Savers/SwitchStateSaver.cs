using Game.Core.Components.Interaction;
using UnityEngine;

namespace Game.Core.Services.SceneState.Savers {
    /// <summary>
    /// Saves and restores the isDisabled flag of a Switch component.
    /// Suitable for single-use switches like the Helm — once disabled, stays disabled.
    /// State is captured on scene leave (snapshot-on-leave).
    /// </summary>
    public sealed class SwitchStateSaver : StateSaverBase {
        private Switch sw;

        public override string Slot => "switch";

        private void Awake() {
            sw = GetComponent<Switch>();
        }

        public override void Capture(IStateWriter w) {
            w.SetBool("disabled", sw.isDisabled);
        }

        public override void Restore(IStateReader r) {
            if (r.TryGetBool("disabled", out var disabled)) {
                sw.isDisabled = disabled;
            }
        }
    }
}
