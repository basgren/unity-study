using System;
using Game.Core.Components.Damage;
using UnityEngine;

namespace Game.Core.Services {
    /// <summary>
    /// Routes boss-fight engagement signals from gameplay to UI.
    /// A boss calls <see cref="EngageBoss"/> when its health bar should appear and
    /// <see cref="DisengageBoss"/> when it should hide. The shield API is independent
    /// so a shield can come and go multiple times within a single boss encounter.
    /// </summary>
    public class BossFightService : MonoBehaviour {
        public Damageable Boss { get; private set; }
        public Damageable Shield { get; private set; }

        public event Action<Damageable> OnBossEngaged;
        public event Action OnBossDisengaged;
        public event Action<Damageable> OnShieldEngaged;
        public event Action OnShieldDisengaged;

        public void EngageBoss(Damageable boss) {
            if (boss == null) {
                return;
            }

            // Defensive: if a previous boss is still engaged (e.g., scene reload race),
            // disengage it first so listeners stay consistent.
            if (Boss != null) {
                DisengageBoss();
            }

            Boss = boss;
            OnBossEngaged?.Invoke(boss);
        }

        public void DisengageBoss() {
            if (Boss == null) {
                return;
            }

            // Drop the shield first so listeners see a consistent teardown order.
            if (Shield != null) {
                DisengageShield();
            }

            Boss = null;
            OnBossDisengaged?.Invoke();
        }

        public void EngageShield(Damageable shield) {
            if (shield == null) {
                return;
            }

            if (Shield != null) {
                DisengageShield();
            }

            Shield = shield;
            OnShieldEngaged?.Invoke(shield);
        }

        public void DisengageShield() {
            if (Shield == null) {
                return;
            }

            Shield = null;
            OnShieldDisengaged?.Invoke();
        }
    }
}
