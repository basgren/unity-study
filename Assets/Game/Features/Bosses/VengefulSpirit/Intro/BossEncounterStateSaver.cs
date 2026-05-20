using Game.Core.Services.SceneState;
using Game.Core.Services.SceneState.Savers;
using UnityEngine;

namespace Game.Features.Bosses.VengefulSpirit.Intro {
    /// <summary>
    /// Persists whether the boss encounter has been won (a single bool — the one true fact the
    /// whole room derives from). On restore, when the flag is set, hands off to
    /// <see cref="BossIntroCutscene.ApplyDefeatedInstant"/> so the room appears already cleared
    /// (candles lit, start trigger disabled, boss removed) without replaying the intro.
    /// State is captured on scene leave (snapshot-on-leave), mirroring SwitchableStateSaver.
    /// </summary>
    public sealed class BossEncounterStateSaver : StateSaverBase {
        private BossIntroCutscene encounter;

        public override string Slot => "bossEncounter";

        private void Awake() {
            encounter = GetComponent<BossIntroCutscene>();
        }

        public override void Capture(IStateWriter w) {
            w.SetBool("defeated", encounter.Defeated);
        }

        public override void Restore(IStateReader r) {
            if (r.TryGetBool("defeated", out var defeated) && defeated) {
                encounter.ApplyDefeatedInstant();
            }
        }
    }
}
