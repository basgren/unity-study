#if UNITY_EDITOR
using System;
using UnityEditor;

namespace Game.Features.Portals.Common.Editor {
    /// <summary>
    /// Watches for scene rename/move and prompts to repair cached portal SceneReferences.
    /// </summary>
    public sealed class PortalSceneReferenceAutoRepairPostprocessor : AssetPostprocessor {
        private static bool repairPromptScheduled;
        private static bool detectedSceneChange;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths) {

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
                    "Portals: Scene renamed/moved",
                    "A scene was renamed/moved. Portal scene references may need cache repair.\n\n" +
                    "Do you want to repair Portal Scene References now?\n" +
                    "(You can always run it later via Tools/Portals/Repair Scene References.)",
                    "Repair now",
                    "Not now"
                );

                if (!doRepair) {
                    return;
                }

                PortalSceneReferenceRepair.RepairAllPortalSceneReferences(showProgressBar: true);
                UnityEngine.Debug.Log("Portals: scene references repaired after scene rename/move.");
            };
        }
    }
}
#endif
