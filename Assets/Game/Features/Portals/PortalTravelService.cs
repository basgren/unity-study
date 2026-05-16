using System;
using System.Collections;
using Game.Core.Bootstrap;
using Game.Core.Services.Scene;
using Game.Features.Characters.Hero;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Features.Portals {
    /// <summary>
    /// Generic travel service shared by all portal kinds (Doors, Entrances, ...).
    /// Owns the fade, scene load and player teleport logic. Type-specific lookup is provided
    /// by the caller via a finder delegate so this service never depends on a concrete portal type.
    /// </summary>
    public static class PortalTravelService {
        private const float FadeDuration = 0.25f;
        private const int MaxCameraSnapWaitFrames = 8;

        /// <summary>
        /// Locates a portal with the given id in the specified scene.
        /// </summary>
        public delegate IPortal PortalFinder(Scene scene, string id);

        /// <summary>
        /// True while a portal travel sequence is in progress (between fade-out start and fade-in complete).
        /// Auto-triggered portals (e.g. Entrance) check this to avoid re-firing when the player overlaps the
        /// destination collider after teleport.
        /// </summary>
        public static bool IsTraveling { get; private set; }

        /// <summary>
        /// Starts the travel sequence from <paramref name="fromPortal"/> to its destination portal.
        /// </summary>
        public static void Travel(IPortal fromPortal, PortalFinder finder) {
            if (fromPortal == null) {
                return;
            }

            if (finder == null) {
                throw new ArgumentNullException(nameof(finder));
            }

            if (IsTraveling) {
                return;
            }

            IsTraveling = true;

            var targetSceneName = fromPortal.TargetScene.GetSceneName();
            var targetId = fromPortal.TargetId;
            var currentScene = fromPortal.gameObject.scene;

            // Make sure controls/damage are disabled even if the caller did not arm them earlier.
            // Doors call BeginTravelTransition before their open animation, but Entrances trigger
            // automatically and rely on this safety call.
            SetHeroTransitionState(false);

            G.Screen.RunWhenFadeOut(
                FadeDuration,
                FadeDuration,
                () => {
                    return currentScene.name == targetSceneName
                        ? TeleportWithDelay(currentScene, targetId, fromPortal, finder)
                        : LoadSceneAndTeleportPlayer(targetSceneName, targetId, fromPortal, finder);
                },
                () => {
                    SetHeroTransitionState(true);
                    IsTraveling = false;
                }
            );
        }

        private static IEnumerator TeleportWithDelay(Scene targetScene, string portalId, IPortal fromPortal,
            PortalFinder finder) {
            var targetPortal = finder(targetScene, portalId);

            TeleportPlayerToPortal(targetPortal);

            // Keep the screen faded while Cinemachine applies teleport warp in the loaded scene.
            yield return WaitForCameraSnapAfterTeleport();
            targetPortal?.NotifyEntered();
            fromPortal.NotifyEntered();
        }

        private static IEnumerator LoadSceneAndTeleportPlayer(string sceneName, string portalId, IPortal fromPortal,
            PortalFinder finder) {
            // SceneTravelService fires BeforeUnload (state capture) before loading the new scene.
            yield return G.SceneTravel.LoadScene(sceneName, new SceneLoadOptions {
                PostLoad = scene => TeleportWithDelay(scene, portalId, fromPortal, finder),
            });
        }

        private static void TeleportPlayerToPortal(IPortal targetPortal) {
            if (targetPortal == null) {
                Debug.LogWarning("Target portal not found during travel.");
                return;
            }

            var playerController = G.Hero.Controller;
            if (playerController == null) {
                // Fallback for cases where the hero service has not registered yet.
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) {
                    playerController = player.GetComponent<PlayerController>();
                }
            }

            if (playerController == null) {
                Debug.LogWarning("PlayerController not found during portal travel.");
                return;
            }

            var targetPosition = targetPortal.GetEntryPosition();
            var previousPosition = playerController.transform.position;
            playerController.TeleportTo(targetPosition);
            G.Camera?.NotifyTargetTeleported(playerController.transform, targetPosition - previousPosition);
        }

        private static IEnumerator WaitForCameraSnapAfterTeleport() {
            if (G.Camera == null) {
                yield break;
            }

            for (int i = 0; i < MaxCameraSnapWaitFrames; i++) {
                if (G.Camera.TryApplyPendingTeleports()) {
                    yield break;
                }

                yield return null;
            }
        }

        private static void SetHeroTransitionState(bool isEnabled) {
            var playerController = G.Hero.Controller;
            if (playerController == null) {
                return;
            }

            playerController.SetCanTakeDamage(isEnabled);
            playerController.SetControlsEnabled(isEnabled);
        }
    }
}
