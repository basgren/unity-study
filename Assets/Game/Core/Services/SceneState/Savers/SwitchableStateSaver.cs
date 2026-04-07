using Core.Components.Interaction;
using UnityEngine;

namespace Game.Core.Services.SceneState.Savers {
    /// <summary>
    /// Saves and restores the IsActive state of a SwitchableBase component.
    /// Suitable for objects toggled by a Switch, such as StoneDoors (once opened, stays open).
    /// State is captured on scene leave (snapshot-on-leave).
    /// </summary>
    public sealed class SwitchableStateSaver : StateSaverBase {
        private SwitchableBase switchable;

        public override string Slot => "switchable";

        private void Awake() {
            switchable = GetComponent<SwitchableBase>();
        }

        public override void Capture(IStateWriter w) {
            w.SetBool("active", switchable.IsActive);
        }

        public override void Restore(IStateReader r) {
            if (r.TryGetBool("active", out var active)) {
                switchable.IsActive = active;
            }
        }
    }
}
