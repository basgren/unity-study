using System.Collections;
using Core.Audio;
using Game.Core.Bootstrap;
using Game.Core.Services.Scene;
using Game.Features.Hero;
using UnityEngine;

namespace Game.Features.Props.Ship {
    /// <summary>
    /// Final "ship departure" cutscene. Started when the Clank dialog raises the configured
    /// event (a <see cref="Core.Models.Dialog.DialogActionType.RaiseEvent"/> action on the
    /// accept node).
    ///
    /// Sequence:
    ///  1. Start the ending music immediately (plays under Clank's final line).
    ///  2. Wait for the dialog/menu to fully close (which restores player controls).
    ///  3. Disable player controls and walk the hero (X only) onto the ship's stand point.
    ///  4. Seat the hero on the ship, hold for <see cref="standDuration"/>, then raise the sails.
    ///  5. Parent the hero and CaptainClack to the ship; it accelerates, sails right and bobs while
    ///     the credits roll plays.
    ///  6. Fade the music and screen out, then return to the main menu (which fades back in).
    ///
    /// One-shot per scene load.
    /// </summary>
    public class ShipDepartureCutscene : MonoBehaviour {
        // Hero reaches the target within roughly one frame of movement; this early-out
        // distance avoids a needless scripted walk when the hero is already on the point.
        private const float ReachThreshold = 0.1f;

        [Header("Participants")]
        [SerializeField]
        private Ship ship;

        [Tooltip("Point the hero walks to and stands on. The hero is parented to the ship once it sails.")]
        [SerializeField]
        private Transform playerStandPoint;

        [Tooltip("CaptainClack, parented to the ship when it sails so he departs along with the hero.")]
        [SerializeField]
        private Transform captainClack;

        [Tooltip("End-credits sequence played while the ship sails. Optional.")]
        [SerializeField]
        private CreditsRoll creditsRoll;

        [Header("Trigger")]
        [Tooltip("Event id raised by the dialog (RaiseEvent action) that starts this cutscene. " +
                 "Must match the stringParam used in the dialog node.")]
        [SerializeField]
        private string triggerEventId = "clank_ship_departure";

        [Header("Audio")]
        [Tooltip("Music that starts the moment the cutscene begins.")]
        [SerializeField]
        private AudioCue departureMusic;

        [Tooltip("Seconds over which the music fades out before returning to the main menu.")]
        [SerializeField]
        private float musicFadeDuration = 2f;

        [Header("Scene")]
        [Tooltip("Main menu scene to return to after the credits.")]
        [SerializeField]
        private SceneReference mainMenuScene;

        [Header("Timing (seconds)")]
        [Tooltip("How long the hero stands on the ship before the sails are raised.")]
        [SerializeField]
        private float standDuration = 2f;
        
        [SerializeField]
        [Tooltip("Time after ship starts sailing and before credits start rolling.")]
        private float timeBeforeCredits = 3f;

        [Tooltip("How long the ship sails to the right before the screen fades out. The credits " +
                 "roll plays concurrently within this window, so the sail still happens even if " +
                 "no credits are assigned.")]
        [SerializeField]
        private float sailDuration = 10f;

        [Tooltip("Duration of the final fade to black before the menu loads.")]
        [SerializeField]
        private float fadeOutDuration = 2f;

        [Tooltip("Duration of the fade-in once the main menu scene has loaded.")]
        [SerializeField]
        private float menuFadeInDuration = 1f;
        
        private bool started;

        private void OnEnable() {
            G.Dialog.EventRaised += HandleDialogEvent;
        }

        private void OnDisable() {
            G.Dialog.EventRaised -= HandleDialogEvent;
        }

        private void HandleDialogEvent(string eventId) {
            if (started || eventId != triggerEventId) {
                return;
            }

            started = true;
            StartCoroutine(Run());
        }

        private IEnumerator Run() {
            // Let the dialog's last line play and the menu fully close before taking over.
            // The menu re-enables player controls on close, so we wait it out and then disable
            // them ourselves to avoid a one-frame window where the player could move.
            yield return new WaitWhile(() => G.Menu != null && G.Menu.IsAnyWindowOpen);

            var hero = G.Hero != null ? G.Hero.Controller : null;
            if (hero == null) {
                yield break;
            }

            hero.SetControlsEnabled(false);

            yield return WalkHeroToX(hero, playerStandPoint.position.x);

            // Music swells while Clank delivers his final line.
            if (departureMusic != null && G.Audio != null) {
                G.Audio.SetLevelMusic(departureMusic);
            }
            
            // Seat the hero on the ship's stand point and stop his movement/gravity so he stands put.
            hero.BeginScriptedFlight(true);
            hero.SetVelocity(Vector2.zero);
            hero.SetFacing(1);
            hero.transform.position = playerStandPoint.position;

            yield return new WaitForSeconds(standDuration);

            ship.SetSail();
            ship.BeginSailing();

            // Parent the riders to the ship so they move and bob with it — no per-frame tracking.
            AttachRider(hero.transform);
            AttachRider(captainClack);

            yield return new WaitForSeconds(timeBeforeCredits);
            
            // Roll the credits concurrently within the sail window (don't block on them), so the
            // ship sails for the full sailDuration whether or not credits are assigned/filled in.
            if (creditsRoll != null) {
                StartCoroutine(creditsRoll.Play());
            }

            yield return new WaitForSeconds(sailDuration);

            // Fade the music and screen out, then return to the main menu. The scene load and
            // fade-in run on persistent services (SceneTravelService / ScreenService), so they
            // survive this object being destroyed when the current scene unloads.
            if (G.Audio != null) {
                G.Audio.ClearLevelMusic(musicFadeDuration);
            }

            if (G.Screen != null) {
                yield return G.Screen.FadeOutCoroutine(fadeOutDuration);
            }

            var fadeIn = menuFadeInDuration;
            G.SceneTravel.LoadScene(mainMenuScene.GetSceneName(), new SceneLoadOptions {
                SkipStateCapture = true,
                PostLoad = _ => G.Screen.FadeInCoroutine(fadeIn)
            });
        }

        private void AttachRider(Transform rider) {
            if (rider == null) {
                return;
            }

            // Stop the rider's physics so the parent transform fully drives it. Both the hero and
            // CaptainClack have a Dynamic Rigidbody2D that would otherwise keep falling / colliding
            // once the ship starts moving and bobbing.
            if (rider.TryGetComponent<Rigidbody2D>(out var rb)) {
                rb.simulated = false;
            }

            rider.SetParent(ship.transform, worldPositionStays: true);
        }

        private IEnumerator WalkHeroToX(PlayerController hero, float targetX) {
            if (Mathf.Abs(targetX - hero.transform.position.x) <= ReachThreshold) {
                yield break;
            }

            var dirSign = targetX > hero.transform.position.x ? 1 : -1;
            hero.BeginScriptedWalk(dirSign);

            // Walk until the hero reaches the target X or overshoots it (sign of the remaining
            // distance flips), since per-frame steps may skip past the threshold band.
            while (true) {
                var remaining = targetX - hero.transform.position.x;
                var reached = Mathf.Abs(remaining) <= ReachThreshold;
                var overshot = (remaining > 0f ? 1 : -1) != dirSign;

                if (reached || overshot) {
                    break;
                }

                yield return null;
            }

            hero.EndScriptedWalk();
        }
    }
}
