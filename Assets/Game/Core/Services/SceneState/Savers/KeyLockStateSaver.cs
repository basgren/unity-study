using Game.Core.Components.Interaction;
using UnityEngine;

namespace Game.Core.Services.SceneState.Savers {
    /// <summary>
    /// Saves and restores whether a <see cref="KeyLock"/> has been unlocked, so a key-gated door stays
    /// open for the rest of the game once its single-use key has been consumed. State is captured on
    /// scene leave (snapshot-on-leave). Typically used with a <c>Persistent</c> tier StateRoot.
    /// </summary>
    public sealed class KeyLockStateSaver : StateSaverBase {
        private KeyLock keyLock;

        public override string Slot => "keyLock";

        private void Awake() {
            keyLock = GetComponent<KeyLock>();
        }

        public override void Capture(IStateWriter w) {
            // A lock with no required key is always open — there is no unlock state worth persisting.
            if (!keyLock.HasRequiredKey) {
                return;
            }

            w.SetBool("unlocked", keyLock.IsUnlocked);
        }

        public override void Restore(IStateReader r) {
            // Mirror Capture: ignore any saved state for always-open (keyless) locks.
            if (!keyLock.HasRequiredKey) {
                return;
            }

            if (r.TryGetBool("unlocked", out var unlocked)) {
                keyLock.IsUnlocked = unlocked;
            }
        }
    }
}
