#if UNITY_EDITOR
using System;
using UnityEditor;

namespace Game.Doors.Editor {
    /// <summary>
    /// Automatically repairs cached scene paths in door links after a scene is moved/renamed.
    /// This keeps SceneReference.scenePath in sync with SceneReference.sceneGuid.
    /// </summary>
    public sealed class DoorSceneReferenceAutoRepairPostprocessor : AssetPostprocessor {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths) {

            var changedAnyScene = false;

            for (var i = 0; i < movedAssets.Length; i++) {
                if (movedAssets[i].EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) {
                    changedAnyScene = true;
                    break;
                }
            }

            if (!changedAnyScene) {
                return;
            }

            DoorSceneReferenceRepair.RepairAllDoorSceneReferences(showProgressBar: true);
            UnityEngine.Debug.Log("Doors: scene rename/move detected. Door scene references were repaired.");
        }
    }
}
#endif
