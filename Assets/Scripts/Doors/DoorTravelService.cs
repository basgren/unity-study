using System.Collections;
using Core.Services;
using PixelCrew.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Doors {
    public static class DoorTravelService {
        public static void Travel(Door fromDoor) {
            var link = fromDoor.Link;
            var targetSceneName = link.TargetScene.GetSceneName();
            var targetDoorId = link.TargetDoorId;
            var currentScene = fromDoor.gameObject.scene;

            G.Screen.RunWhenFadeOut(
                0.25f,
                0.25f,
                () => {
                    return currentScene.name == targetSceneName
                        ? TeleportWithDelay(currentScene, targetDoorId, fromDoor)
                        : LoadSceneAndTeleportPlayer(targetSceneName, targetDoorId, fromDoor);
                }
            );
        }

        private static IEnumerator TeleportWithDelay(Scene targetScene, string doorId, Door fromDoor) {
            var targetDoor = DoorUtils.FindDoorByIdInScene(targetScene, doorId);
            
            TeleportPlayerToDoor(targetDoor);
            
            // Wait a little bit to make scene settle and make small delay, as quick fade-out + fade-in
            // looks like flash.
            yield return new WaitForSecondsRealtime(0.4f);
            targetDoor.NotifyEntered();
            fromDoor.NotifyEntered();
        }
        
        private static IEnumerator LoadSceneAndTeleportPlayer(string sceneName, string doorId, Door fromDoor) {
            AsyncOperation sceneLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

            while (!sceneLoad.isDone) {
                yield return null;
            }
            
            var scene = SceneManager.GetSceneByName(sceneName);

            yield return TeleportWithDelay(scene, doorId, fromDoor);
        }

        private static void TeleportPlayerToDoor(Door targetDoor) {
            // We assume that player is always present and doorId valid
            // TODO: [BG] Find better way of finding player object. Maybe some service? 
            var player = GameObject.FindGameObjectWithTag("Player");
            var playerController = player.GetComponent<PlayerController>();

            // TODO: [BG] Also move camera immediately to the player after teleportation.
            playerController.TeleportTo(targetDoor.GetEntryPosition());
        }
    }
}
