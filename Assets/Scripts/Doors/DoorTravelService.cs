using PixelCrew.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Doors {
    public static class DoorTravelService {
        private static string pendingSceneName;
        private static string pendingDoorId;
        private static bool subscribed;

        public static void Travel(Door fromDoor) {
            var link = fromDoor.Link;

            var targetSceneName = link.TargetScene.GetSceneName();
            var targetDoorId = link.TargetDoorId;

            var currentScene = fromDoor.gameObject.scene;
            if (currentScene.name == targetSceneName) {
                var targetDoor = DoorUtils.FindDoorByIdInScene(currentScene, targetDoorId);
                TeleportPlayerToDoor(targetDoor);
                return;
            }

            pendingSceneName = targetSceneName;
            pendingDoorId = targetDoorId;

            if (!subscribed) {
                SceneManager.sceneLoaded += OnSceneLoaded;
                subscribed = true;
            }

            SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            if (scene.name != pendingSceneName) {
                return;
            }

            var targetDoor = DoorUtils.FindDoorByIdInScene(scene, pendingDoorId);
            TeleportPlayerToDoor(targetDoor);

            pendingSceneName = null;
            pendingDoorId = null;
        }

        private static void TeleportPlayerToDoor(Door targetDoor) {
            // We assume that player is always present and doorId valid
            // TODO: [BG] Find better way of finding player object. Maybe some service?  
            var player = GameObject.FindGameObjectWithTag("Player");
            var playerController = player.GetComponent<PlayerController>();

            playerController.TeleportTo(targetDoor.GetEntryPosition());
        }
    }
}
