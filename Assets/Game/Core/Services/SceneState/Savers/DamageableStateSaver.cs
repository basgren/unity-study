using Game.Core.Bootstrap;
using Game.Core.Components.Damage;

namespace Game.Core.Services.SceneState.Savers {
    /// <summary>
    /// Saves and restores the current health of a Damageable component.
    /// Useful for enemies that have been weakened but not killed (typically tier = Session,
    /// so health resets when the player rests at a bonfire).
    ///
    /// Health changes are pushed immediately via OnHealthChanged so state is accurate
    /// even before the scene unloads.
    /// </summary>
    public sealed class DamageableStateSaver : StateSaverBase {
        private Damageable damageable;
        private StateRoot stateRoot;

        public override string Slot => "damageable";

        private void Awake() {
            damageable = GetComponent<Damageable>();
            stateRoot = GetComponent<StateRoot>();
        }

        private void OnEnable() {
            if (damageable != null) {
                damageable.OnHealthChanged += OnHealthChanged;
            }
        }

        private void OnDisable() {
            if (damageable != null) {
                damageable.OnHealthChanged -= OnHealthChanged;
            }
        }

        private void OnHealthChanged(float health) {
            if (stateRoot != null && G.SceneState != null) {
                G.SceneState.PushSlot(stateRoot, Slot, w => w.SetFloat("hp", health));
            }
        }

        public override void Capture(IStateWriter w) {
            w.SetFloat("hp", damageable.Health);
        }

        public override void Restore(IStateReader r) {
            if (r.TryGetFloat("hp", out var hp)) {
                damageable.SetHealth(hp);
            }
        }
    }
}
