#if UNITY_EDITOR
using System;
using UnityEditor;

namespace Game.Doors.Editor {
    public sealed class DoorSceneReferenceAutoRepairPostprocessor : AssetPostprocessor {
        private static bool repairPromptScheduled;
        private static bool detectedSceneChange;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths) {

            // Detect scene rename/move
            for (var i = 0; i < movedAssets.Length; i++) {
                if (movedAssets[i].EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) {
                    detectedSceneChange = true;
                    break;
                }
            }

            if (!detectedSceneChange) {
                return;
            }

            // Schedule prompt once (do not run immediately inside postprocessor)
            if (repairPromptScheduled) {
                return;
            }

            repairPromptScheduled = true;

            EditorApplication.delayCall += () => {
                repairPromptScheduled = false;

                if (!detectedSceneChange) {
                    return;
                }

                detectedSceneChange = false;

                var doRepair = EditorUtility.DisplayDialog(
                    "Doors: Scene renamed/moved",
                    "A scene was renamed/moved. Door scene references may need cache repair.\n\n" +
                    "Do you want to repair Door Scene References now?\n" +
                    "(You can always run it later via Tools/Doors/Repair Door Scene References.)",
                    "Repair now",
                    "Not now"
                );

                if (!doRepair) {
                    return;
                }

                DoorSceneReferenceRepair.RepairAllDoorSceneReferences(showProgressBar: true);
                UnityEngine.Debug.Log("Doors: Door scene references repaired after scene rename/move.");
            };
        }
    }
}
#endif
