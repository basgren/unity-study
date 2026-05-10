using System.Collections;
using Core.Audio;
using Core.Components.Base2D;
using Game.Core.Bootstrap;
using Game.Features.Bosses.VengefulSpirit.Teleport;
using Game.Features.Characters.Hero;
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

        [Header("Candles")]
        [Tooltip("Candles in the row that lights up first.")]
        [SerializeField]
        private CandleController[] firstRowCandles;

        [Tooltip("Candles in the row that lights up second.")]
        [SerializeField]
        private CandleController[] secondRowCandles;

        [Header("Audio")]
        [Tooltip("One-shot SFX played just before the spirit appears.")]
        [SerializeField]
        private AudioCue laughCue;

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

        private bool started;

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

            yield return new WaitForSeconds(beforeFirstRow);
            
            if (laughCue != null && G.Audio != null) {
                G.Audio.Play2D(laughCue);
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

            if (boss != null && boss.Damageable != null && G.BossFight != null) {
                G.BossFight.EngageBoss(boss.Damageable);
            }

            // Hand off to the AI: enabling resets its warmup timer in OnEnable, so the
            // player gets a beat before the first attack lands.
            if (bossAI != null) {
                bossAI.enabled = true;
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
    }
}
