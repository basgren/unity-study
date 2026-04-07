using System.Collections;
using Game.Core.Bootstrap;
using Game.Core.Services.Scene;
using Game.Features.Characters.Hero;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Features.Doors {
    public static class DoorTravelService {
        private const float DoorFadeDuration = 0.25f;
        private const int MaxCameraSnapWaitFrames = 8;

        public static void Travel(Door fromDoor) {
            var link = fromDoor.Link;
            var targetSceneName = link.TargetScene.GetSceneName();
            var targetDoorId = link.TargetDoorId;
            var currentScene = fromDoor.gameObject.scene;

            G.Screen.RunWhenFadeOut(
                DoorFadeDuration,
                DoorFadeDuration,
                () => {
                    return currentScene.name == targetSceneName
                        ? TeleportWithDelay(currentScene, targetDoorId, fromDoor)
                        : LoadSceneAndTeleportPlayer(targetSceneName, targetDoorId, fromDoor);
                },
                () => SetHeroTransitionState(true)
            );
        }

        private static IEnumerator TeleportWithDelay(Scene targetScene, string doorId, Door fromDoor) {
            var targetDoor = DoorUtils.FindDoorByIdInScene(targetScene, doorId);

            TeleportPlayerToDoor(targetDoor);

            // Keep the screen faded while Cinemachine applies teleport warp in the loaded scene.
            yield return WaitForCameraSnapAfterTeleport();
            targetDoor.NotifyEntered();
            fromDoor.NotifyEntered();
        }

        private static IEnumerator LoadSceneAndTeleportPlayer(string sceneName, string doorId, Door fromDoor) {
            // SceneTravelService fires BeforeUnload (state capture) before loading the new scene.
            yield return G.SceneTravel.LoadScene(sceneName, new SceneLoadOptions {
                PostLoad = scene => TeleportWithDelay(scene, doorId, fromDoor),
            });
        }

        private static void TeleportPlayerToDoor(Door targetDoor) {
            var playerController = G.Hero.Controller;
            if (playerController == null) {
                // Fallback for cases where the hero service has not registered yet.
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) {
                    playerController = player.GetComponent<PlayerController>();
                }
            }

            if (playerController == null) {
                Debug.LogWarning("PlayerController not found during door travel.");
                return;
            }

            var targetPosition = targetDoor.GetEntryPosition();
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
