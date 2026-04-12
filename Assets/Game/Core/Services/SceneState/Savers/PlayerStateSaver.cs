using Game.Core.Bootstrap;
using Game.Core.Models.Shop;
using Game.Features.Characters.Hero;
using UnityEngine;

namespace Game.Core.Services.SceneState.Savers {
    /// <summary>
    /// Saves and restores hero state that should survive scene reloads:
    /// equipped weapon flag and stat upgrade levels.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerStateSaver : StateSaverBase {
        private const string KeyArmed = "armed";
        private const string KeyStatHealth = "stat_health";
        private const string KeyStatMelee = "stat_melee";
        private const string KeyStatThrow = "stat_throw";
        private const string KeyCurrentHealth = "currentHealth";

        private PlayerController controller;

        public override string Slot => "player";

        private void Awake() {
            controller = GetComponent<PlayerController>();
        }

        public override void Capture(IStateWriter w) {
            w.SetBool(KeyArmed, controller.IsArmed);

            var state = G.Game.playerState;
            w.SetInt(KeyStatHealth, state.GetStatLevel(StatId.Health));
            w.SetInt(KeyStatMelee, state.GetStatLevel(StatId.MeleeDamage));
            w.SetInt(KeyStatThrow, state.GetStatLevel(StatId.ThrowDamage));
            w.SetFloat(KeyCurrentHealth, state.currentHealth);
        }

        public override void Restore(IStateReader r) {
            if (r.TryGetBool(KeyArmed, out var armed)) {
                controller.RestorePersistentState(armed);
            }

            var state = G.Game.playerState;
            if (r.TryGetInt(KeyStatHealth, out var health)) {
                state.SetStatLevel(StatId.Health, health);
            }

            if (r.TryGetInt(KeyStatMelee, out var melee)) {
                state.SetStatLevel(StatId.MeleeDamage, melee);
            }

            if (r.TryGetInt(KeyStatThrow, out var throwDmg)) {
                state.SetStatLevel(StatId.ThrowDamage, throwDmg);
            }

            if (r.TryGetFloat(KeyCurrentHealth, out var hp)) {
                state.currentHealth = hp;
            }

            // Push restored stats and HP to the live damager/health components.
            controller.ApplyCurrentStats();
            controller.Damageable.SetHealth(state.currentHealth);
        }
    }
}
