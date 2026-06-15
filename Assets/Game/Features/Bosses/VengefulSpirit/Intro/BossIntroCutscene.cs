using System.Collections;
using Core.Audio;
using Game.Core.Bootstrap;
using Game.Core.Components.Base2D;
using Game.Features.Bosses.VengefulSpirit.Teleport;
using Game.Features.Characters.Hero;
using Game.Features.Hero;
using Game.Features.PirateShip;
using UnityEngine;

namespace Game.Features.Bosses.VengefulSpirit.Intro {
    /// <summary>
    /// One-shot scripted intro for the Vengeful Spirit boss room. Wired to a scene
    /// trigger volume (typically a TriggerEnterComponent) whose UnityEvent calls
    /// <see cref="Begin"/> when the player crosses the threshold.
    ///
    /// Sequence:
    ///  1. Block player input.
    ///  2. Wait, light the first row of candles.
    ///  3. Wait, light the second row of candles.
    ///  4. Wait, play the laughing SFX.
    ///  5. Boss teleports into the chosen anchor (uses the standard fade-out → reposition
    ///     → fade-in flow). Player turns to face the spirit (Right).
    ///  6. Wait, boss fades out in place.
    ///  7. Start boss music, return controls, engage the boss fight, enable the AI.
    ///
    /// One-shot per scene-load: re-entering the trigger after the first run does nothing.
    /// </summary>
    public class BossIntroCutscene : MonoBehaviour {
        [Header("Participants")]
        [SerializeField]
        private VengefulSpirit boss;

        [Tooltip("AI that picks boss patterns. Held disabled during the cutscene so the boss " +
                 "stays idle off-camera; re-enabled at the end so the fight begins.")]
        [SerializeField]
        private VengefulSpiritAI bossAI;

        [Tooltip("Anchor where the spirit fades in. Its FacingDir determines which way the " +
                 "spirit faces on arrival.")]
        [SerializeField]
        private TeleportAnchor introAnchor;

        [Tooltip("Trigger volume that starts the encounter. Disabled once the boss is defeated " +
                 "so the cutscene cannot replay when the player returns to a cleared room.")]
        [SerializeField]
        private GameObject startTrigger;

        [Header("Candles")]
        [Tooltip("Candles in the row that lights up first.")]
        [SerializeField]
        private CandleController[] firstRowCandles;

        [Tooltip("Candles in the row that lights up second.")]
        [SerializeField]
        private CandleController[] secondRowCandles;

        [Header("Audio")]
        [Tooltip("Dedicated scene AudioSource that plays the laugh. Use a scene AudioSource " +
                 "(rather than G.Audio.Play2D) so designers can attach an AudioReverbFilter " +
                 "to it — that filter exposes named presets like Cave / Hangar / StoneRoom.")]
        [SerializeField]
        private AudioSource laughSource;

        [Tooltip("Background track that starts when the cutscene ends.")]
        [SerializeField]
        private AudioCue bossMusicCue;

        [Header("Timing (seconds)")]
        [SerializeField]
        private float beforeFirstRow = 2f;

        [SerializeField]
        private float beforeSecondRow = 1f;

        [SerializeField]
        private float beforeLaugh = 1f;

        [SerializeField]
        private float spiritVisibleDuration = 2f;

        [Tooltip("Hidden hold inside the boss's fade-out → reposition → fade-in run. Zero " +
                 "makes the boss reappear at the anchor as soon as the fade-out completes.")]
        [SerializeField]
        private float spiritHiddenHold;

        [Tooltip("How long the boss music takes to fade to silence after the boss dies.")]
        [SerializeField]
        private float musicFadeOutSeconds = 2f;

        [Header("Progression")]
        [Tooltip("Player-state flag raised when this boss is defeated. Persists in the save " +
                 "file and is readable from other scenes (e.g. to unlock shop stock). Empty " +
                 "raises no flag.")]
        [SerializeField]
        private string defeatedFlag = "VengefulSpiritDefeated";

        private bool started;
        private bool defeated;
        private bool subscribedToDisengage;

        /// <summary>True once the boss has been defeated in this encounter. Persisted by the
        /// sibling BossEncounterStateSaver so the room stays cleared across scene transitions.</summary>
        public bool Defeated => defeated;

        // Hold the AI off until the cutscene hands off. Done in Start (not Awake) so the
        // boss's own Awake — which calls SyncControlSourceEnabled and would re-enable the AI —
        // has already run by the time we disable it.
        private void Start() {
            if (bossAI != null) {
                bossAI.enabled = false;
            }
        }

        /// <summary>
        /// Cutscene entry point. Wire your TriggerEnterComponent's UnityEvent to this method.
        /// Subsequent calls after the first are no-ops, so the cutscene cannot replay.
        /// </summary>
        public void Begin() {
            if (started) {
                return;
            }
            started = true;
            StartCoroutine(RunIntroCo());
        }

        private IEnumerator RunIntroCo() {
            PlayerController hero = G.Hero != null ? G.Hero.Controller : null;
            Facing2D heroFacing = hero != null ? hero.GetComponent<Facing2D>() : null;

            if (hero != null) {
                hero.SetControlsEnabled(false);
            }

            // Fade the HUD out while the cutscene owns the screen; restored when controls return.
            if (G.Hud != null) {
                G.Hud.Hide();
            }

            yield return new WaitForSeconds(beforeFirstRow);
            
            if (laughSource != null) {
                laughSource.Play();
            }
            
            SetRowLit(firstRowCandles, true);

            yield return new WaitForSeconds(beforeSecondRow);
            SetRowLit(secondRowCandles, true);

            yield return new WaitForSeconds(beforeLaugh);

            // Boss starts off-camera, so the fade-out half of the run is invisible to the
            // player; only the fade-in at the anchor reads. spiritHiddenHold = 0 keeps the
            // reappearance snappy. RequestTeleport early-returns if the boss is busy, so we
            // poll IsBusy afterwards to wait for the run to complete.
            if (boss != null && introAnchor != null) {
                boss.RequestTeleport(introAnchor, spiritHiddenHold);
                yield return new WaitWhile(() => boss.IsBusy);
            }

            if (heroFacing != null) {
                heroFacing.SetDir(FacingDir.Right);
            }

            yield return new WaitForSeconds(spiritVisibleDuration);

            if (boss != null) {
                boss.RequestFadeOutInPlace();
                yield return new WaitWhile(() => boss.IsBusy);
            }

            if (bossMusicCue != null && G.Audio != null) {
                G.Audio.SetLevelMusic(bossMusicCue);
            }

            if (hero != null) {
                hero.SetControlsEnabled(true);
            }

            // Bring the HUD back as the fight begins.
            if (G.Hud != null) {
                G.Hud.Show();
            }

            if (boss != null && boss.Damageable != null && G.BossFight != null) {
                G.BossFight.EngageBoss(boss.Damageable);
                G.BossFight.OnBossDisengaged += HandleBossDisengaged;
                subscribedToDisengage = true;
            }

            // Hand off to the AI: enabling resets its warmup timer in OnEnable, so the
            // player gets a beat before the first attack lands.
            if (bossAI != null) {
                bossAI.enabled = true;
            }
        }

        // BossFightService.DisengageBoss fires when the boss dies (and also on scene
        // unload via VengefulSpirit.OnDisable — calling ClearLevelMusic on shutdown is
        // harmless). One-shot: unsubscribe immediately so a later EngageBoss / death
        // cycle in the same scene wouldn't double-fade.
        private void HandleBossDisengaged() {
            UnsubscribeFromDisengage();
            if (G.Audio != null) {
                G.Audio.ClearLevelMusic(musicFadeOutSeconds);
            }
        }

        private void OnDestroy() {
            UnsubscribeFromDisengage();
        }

        private void UnsubscribeFromDisengage() {
            if (!subscribedToDisengage) {
                return;
            }
            subscribedToDisengage = false;
            if (G.BossFight != null) {
                G.BossFight.OnBossDisengaged -= HandleBossDisengaged;
            }
        }

        private static void SetRowLit(CandleController[] row, bool lit) {
            if (row == null) {
                return;
            }
            for (int i = 0; i < row.Length; i++) {
                if (row[i] != null) {
                    row[i].Lit = lit;
                }
            }
        }

        /// <summary>
        /// Records the encounter as won. Wire the boss's <c>Damageable.onDeath</c> to this so the
        /// sibling BossEncounterStateSaver can persist the cleared state.
        /// </summary>
        public void MarkDefeated() {
            defeated = true;
            RaiseDefeatedFlag();
        }

        /// <summary>
        /// Puts the room straight into its post-victory state with no cutscene, fade, or SFX —
        /// candles lit, start trigger disabled, boss removed. Called by BossEncounterStateSaver on
        /// restore when the boss was already defeated, so re-entering a cleared room never refights.
        /// </summary>
        public void ApplyDefeatedInstant() {
            defeated = true;
            // Restoring a cleared room: ensure the progression flag is set too, in case a
            // save predates the flag. SetFlag is idempotent so re-raising is harmless.
            RaiseDefeatedFlag();
            // Block the intro from running this scene-load even if the trigger is somehow crossed.
            started = true;

            SetRowLitImmediate(firstRowCandles);
            SetRowLitImmediate(secondRowCandles);

            if (startTrigger != null) {
                startTrigger.SetActive(false);
            }

            if (bossAI != null) {
                bossAI.enabled = false;
            }

            // The boss is already beaten — remove it before its first frame so no health bar
            // engages and no death animation plays on a returning visit.
            if (boss != null) {
                boss.gameObject.SetActive(false);
            }
        }

        // Raises the defeated flag on the persistent player state so other scenes
        // (e.g. Rikko's shop) can react to this boss being beaten.
        private void RaiseDefeatedFlag() {
            if (!string.IsNullOrEmpty(defeatedFlag) && G.Game != null) {
                G.Game.playerState.SetFlag(defeatedFlag);
            }
        }

        private static void SetRowLitImmediate(CandleController[] row) {
            if (row == null) {
                return;
            }
            for (int i = 0; i < row.Length; i++) {
                if (row[i] != null) {
                    row[i].SetLitImmediate(true);
                }
            }
        }
    }
}
