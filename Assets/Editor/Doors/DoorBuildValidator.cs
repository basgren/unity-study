#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Editor.Doors {
    /// <summary>
    /// Build-time validation. Fails the build if doors contain broken links or duplicate IDs.
    /// </summary>
    public sealed class DoorBuildValidator : IPreprocessBuildWithReport {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) {
            UnityEngine.Debug.Log("DoorBuildValidator: running...");
            var sb = new StringBuilder();
            var errorCount = 0;

            var sceneGuids = AssetDatabase.FindAssets("t:Scene");
            for (var i = 0; i < sceneGuids.Length; i++) {
                var path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                if (string.IsNullOrWhiteSpace(path)) {
                    continue;
                }

                var already = SceneManager.GetSceneByPath(path);
                var alreadyLoaded = already.IsValid() && already.isLoaded;

                var scene = alreadyLoaded ? already : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try {
                    var errors = DoorValidator.ValidateScene(scene);
                    for (var e = 0; e < errors.Count; e++) {
                        errorCount++;
                        sb.AppendLine(errors[e].Message);
                    }
                }
                finally {
                    if (!alreadyLoaded) {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }

            if (errorCount > 0) {
                throw new BuildFailedException($"Doors validation failed ({errorCount} errors): {sb}");
            }
        }
    }
}
#endif
