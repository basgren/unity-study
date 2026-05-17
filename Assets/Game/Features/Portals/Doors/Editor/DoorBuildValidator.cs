#if UNITY_EDITOR
using System.Text;
using Game.Features.Portals.Common.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Game.Features.Portals.Doors.Editor {
    /// <summary>
    /// Build-time validation. Fails the build if doors contain broken links or duplicate IDs.
    /// </summary>
    public sealed class DoorBuildValidator : IPreprocessBuildWithReport {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) {
            // Disabled: opening every scene during preprocess surfaces unrelated console errors
            // (e.g. Light2D global-light warnings) and dirties scenes. Re-enable when the
            // validator can run without side effects on scene state.
            UnityEngine.Debug.Log("DoorBuildValidator: disabled, skipping.");
            return;

#pragma warning disable CS0162
            UnityEngine.Debug.Log("DoorBuildValidator: running...");
            var kind = PortalKindRegistry.GetForType(typeof(Door));
            if (kind == null) {
                return;
            }

            var sb = new StringBuilder();
            var errorCount = 0;

            var sceneGuids = AssetDatabase.FindAssets("t:Scene");
            for (var i = 0; i < sceneGuids.Length; i++) {
                var path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                if (string.IsNullOrWhiteSpace(path)) {
                    continue;
                }

                PortalEditorUtils.ExecuteInScene(path, scene => {
                    var errors = PortalValidator.ValidateScene(kind, scene);
                    for (var e = 0; e < errors.Count; e++) {
                        errorCount++;
                        sb.AppendLine(errors[e].Message);
                    }
                });
            }

            if (errorCount > 0) {
                throw new BuildFailedException($"Doors validation failed ({errorCount} errors): {sb}");
            }
#pragma warning restore CS0162
        }
    }
}
#endif
