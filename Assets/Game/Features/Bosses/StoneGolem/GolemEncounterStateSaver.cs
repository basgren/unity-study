using Game.Core.Services.SceneState;
using Game.Core.Services.SceneState.Savers;

namespace Game.Features.Bosses.StoneGolem {
    /// <summary>
    /// Persists whether the Stone Golem encounter has been won (a single bool — the one fact the whole
    /// room derives from). On restore, when set, hands off to
    /// <see cref="GolemSceneDirector.ApplyDefeatedInstant"/> so the room appears already cleared
    /// (sanctuary door open, final confiner, boss removed) without replaying the fight. Captured on
    /// scene leave (snapshot-on-leave), mirroring SwitchableStateSaver and VengefulSpirit's
    /// BossEncounterStateSaver.
    ///
    /// Put on the same GameObject as <see cref="GolemSceneDirector"/>, under a Persistent StateRoot.
    /// </summary>
    public sealed class GolemEncounterStateSaver : StateSaverBase {
        private GolemSceneDirector director;

        public override string Slot => "golemEncounter";

        private void Awake() {
            director = GetComponent<GolemSceneDirector>();
        }

        public override void Capture(IStateWriter w) {
            w.SetBool("defeated", director.Defeated);
        }

        public override void Restore(IStateReader r) {
            if (r.TryGetBool("defeated", out var defeated) && defeated) {
                director.ApplyDefeatedInstant();
            }
        }
    }
}
