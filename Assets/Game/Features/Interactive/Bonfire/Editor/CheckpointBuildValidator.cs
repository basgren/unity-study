#if UNITY_EDITOR
using System.Text;
using Game.Core.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Game.Features.Interactive.Bonfire.Editor {
    /// <summary>
    /// Build-time validation. Fails the build if bonfires have invalid or duplicate checkpoint IDs.
    /// </summary>
    public sealed class CheckpointBuildValidator : IPreprocessBuildWithReport {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) {
            UnityEngine.Debug.Log("CheckpointBuildValidator: running...");
            var sb = new StringBuilder();
            var errorCount = 0;

            var sceneGuids = AssetDatabase.FindAssets("t:Scene");
            for (var i = 0; i < sceneGuids.Length; i++) {
                var path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                if (string.IsNullOrWhiteSpace(path)) {
                    continue;
                }

                EditorSceneUtils.ExecuteInScene(path, scene => {
                    var errors = CheckpointValidator.ValidateScene(scene);
                    for (var e = 0; e < errors.Count; e++) {
                        errorCount++;
                        sb.AppendLine(errors[e].Message);
                    }
                });
            }

            if (errorCount > 0) {
                throw new BuildFailedException($"Checkpoint validation failed ({errorCount} errors):\n{sb}");
            }
        }
    }
}
#endif
