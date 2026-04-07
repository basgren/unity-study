using Game.Features.Characters.Hero;
using UnityEngine;

namespace Game.Core.Services.SceneState.Savers {
    /// <summary>
    /// Saves and restores the hero's equipped weapon state so scene reloads keep the player armed.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerStateSaver : StateSaverBase {
        private PlayerController controller;

        public override string Slot => "player";

        private void Awake() {
            controller = GetComponent<PlayerController>();
        }

        public override void Capture(IStateWriter w) {
            w.SetBool("armed", controller.IsArmed);
        }

        public override void Restore(IStateReader r) {
            if (r.TryGetBool("armed", out var armed)) {
                controller.RestorePersistentState(armed);
            }
        }
    }
}
