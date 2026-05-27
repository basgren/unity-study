using Game.Core.Components.Camera;
using Game.Core.Components.Interaction;
using UnityEngine;

namespace Game.Features.Bosses.StoneGolem {
    /// <summary>
    /// Central orchestrator for the Stone Golem encounter room. It does NOT run the fight
    /// (<see cref="StoneGolemAI"/>), the intro drop-in (<see cref="StoneGolemIntro"/>), or the phase-2
    /// cutscene (the ground-break action) — those own their own logic. The director sits on top and
    /// reacts to the encounter's milestone events, driving the camera confiner, the sanctuary door,
    /// and persistence:
    /// <list type="bullet">
    /// <item><description><see cref="OnFloorBroken"/> (wired from the ground-break action's
    /// <c>onFloorBroken</c>) → the confiner switches to the "big" state revealing the lower room.</description></item>
    /// <item><description><see cref="OnBossDefeated"/> (wired from the golem <c>Damageable.onDeath</c>) →
    /// the sanctuary door opens, the confiner switches to the "final" state, and the win is recorded.</description></item>
    /// </list>
    /// The single persisted fact is <see cref="Defeated"/>; on restore into a cleared room
    /// <see cref="ApplyDefeatedInstant"/> derives the whole end state from it (door open, final confiner,
    /// boss removed) with no cutscene — mirroring VengefulSpirit's BossIntroCutscene.
    ///
    /// Phase-2 mid-fight state (broken floor, "big" confiner) is intentionally NOT persisted: if the
    /// player dies the scene reloads and the fight restarts from phase 1.
    /// </summary>
    public class GolemSceneDirector : MonoBehaviour {
        [Header("Participants")]
        [SerializeField]
        [Tooltip("The golem controller. Removed on restore into an already-cleared room.")]
        private StoneGolem golem;

        [SerializeField]
        [Tooltip("The golem selector. Disabled on restore into an already-cleared room.")]
        private StoneGolemAI golemAI;

        [SerializeField]
        [Tooltip("Intro drop-in. Disabled on restore so it does not hold the (removed) golem inert.")]
        private StoneGolemIntro intro;

        [SerializeField]
        [Tooltip("Optional: the SkullFake pickup / its trigger, disabled on restore so a cleared room " +
                 "cannot restart the fight.")]
        private GameObject fightStartTrigger;

        [Header("Camera confiner")]
        [SerializeField]
        [Tooltip("Selector that switches the camera bounding shape between encounter stages.")]
        private CameraConfinerSelector confinerSelector;

        [SerializeField]
        [Tooltip("Confiner state id applied when the floor breaks (phase 2 reveals the lower room).")]
        private string bigConfinerId = "Big";

        [SerializeField]
        [Tooltip("Confiner state id applied once the boss is defeated.")]
        private string finalConfinerId = "Final";

        [Header("Doors")]
        [SerializeField]
        [Tooltip("Sanctuary door opened when the boss is defeated. Its open state is derived from the " +
                 "encounter's Defeated flag, so it needs no separate state saver.")]
        private SwitchableBase sanctuaryDoor;

        private bool defeated;

        /// <summary>True once the boss has been defeated. The single fact the encounter saver persists.</summary>
        public bool Defeated => defeated;

        private void Awake() {
            // The confiner swaps silently no-op without this reference, which is easy to miss in-scene.
            if (confinerSelector == null) {
                Debug.LogWarning("GolemSceneDirector: confinerSelector is not assigned — the camera " +
                                 "confiner will not switch on floor-broken or defeat.", this);
            }
        }

        /// <summary>
        /// Front door for starting the encounter — wire the SkullFake pickup's UnityEvent here instead
        /// of calling <see cref="StoneGolemIntro.Begin"/> directly. Triggers the intro drop-in, which
        /// engages the boss and hands control to the AI. Repeat calls are ignored by the intro itself.
        /// Routing start through the director keeps one place to add start-time coordination later
        /// (music, resetting the confiner to its small state, etc.).
        /// </summary>
        public void StartFight() {
            if (intro != null) {
                intro.Begin();
            }
        }

        /// <summary>
        /// Wire the ground-break action's <c>onFloorBroken</c> UnityEvent here. Switches the camera to
        /// the wider "big" confiner the instant the floor shatters.
        /// </summary>
        public void OnFloorBroken() {
            if (confinerSelector != null) {
                confinerSelector.Select(bigConfinerId);
            }
        }

        /// <summary>
        /// Wire the golem's <c>Damageable.onDeath</c> UnityEvent here. Opens the sanctuary door,
        /// switches to the "final" confiner, and records the win. Runs once.
        /// </summary>
        public void OnBossDefeated() {
            if (defeated) {
                return;
            }

            defeated = true;
            OpenSanctuaryDoor();

            if (confinerSelector != null) {
                confinerSelector.Select(finalConfinerId);
            }
        }

        /// <summary>
        /// Restore path (called by <see cref="GolemEncounterStateSaver"/> when the boss was already
        /// beaten): put the room straight into its cleared state — sanctuary door open, final confiner,
        /// golem removed — with no cutscene, so re-entering never refights.
        /// </summary>
        public void ApplyDefeatedInstant() {
            defeated = true;
            OpenSanctuaryDoor();

            if (confinerSelector != null) {
                confinerSelector.SelectImmediate(finalConfinerId);
            }

            // Stop the encounter from coming alive on a cleared visit.
            if (fightStartTrigger != null) {
                fightStartTrigger.SetActive(false);
            }

            if (intro != null) {
                intro.enabled = false;
            }

            if (golemAI != null) {
                golemAI.enabled = false;
            }

            // The boss is already beaten — remove it before its first frame so no health bar engages
            // and no death animation plays on a returning visit.
            if (golem != null) {
                golem.gameObject.SetActive(false);
            }
        }

        private void OpenSanctuaryDoor() {
            if (sanctuaryDoor != null) {
                sanctuaryDoor.IsActive = true;
            }
        }
    }
}
