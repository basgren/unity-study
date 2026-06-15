#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor.SceneTools {
    /// <summary>
    /// Runs an <see cref="ISceneOperation"/> across a set of scenes by path. Scenes that are not
    /// already open are opened additively, operated on, saved if changed, then closed again; the
    /// original set of open scenes is restored afterward. All messages go to the Unity Console.
    /// </summary>
    public static class SceneOperationRunner {
        public static void Run(ISceneOperation operation, IReadOnlyList<string> scenePaths) {
            if (operation == null || scenePaths == null || scenePaths.Count == 0) {
                Debug.LogWarning("[Scene Tools] Nothing to run: pick an operation and at least one scene.");
                return;
            }

            // Persisting changes saves scenes; let the user save unsaved edits in the open scenes
            // first so the run never silently discards them.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                Debug.Log("[Scene Tools] Run cancelled.");
                return;
            }

            var originallyOpen = GetOpenScenePaths();

            int totalIssues = 0;
            int totalChanges = 0;
            int processed = 0;

            try {
                for (var i = 0; i < scenePaths.Count; i++) {
                    var path = scenePaths[i];
                    if (string.IsNullOrEmpty(path)) {
                        continue;
                    }

                    EditorUtility.DisplayProgressBar(
                        "Scene Tools",
                        $"{operation.DisplayName}: {path}",
                        scenePaths.Count == 0 ? 0f : (float)i / scenePaths.Count);

                    var wasOpen = originallyOpen.Contains(path);
                    var scene = wasOpen
                        ? SceneManager.GetSceneByPath(path)
                        : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

                    if (!scene.IsValid()) {
                        Debug.LogError($"[Scene Tools] Could not open scene '{path}'.");
                        continue;
                    }

                    var log = new ConsoleLog(scene.name);
                    var result = operation.Run(scene, log);
                    totalIssues += result.Issues;
                    totalChanges += result.Changes;
                    processed++;

                    if (operation.Mutates && result.Changes > 0) {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }

                    // Only close scenes the runner opened, so the editor's original layout is preserved.
                    if (!wasOpen) {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
            } finally {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log(
                $"[Scene Tools] {operation.DisplayName}: {processed} scene(s), " +
                $"{totalIssues} issue(s), {totalChanges} change(s).");
        }

        private static HashSet<string> GetOpenScenePaths() {
            var open = new HashSet<string>();
            for (var i = 0; i < SceneManager.sceneCount; i++) {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && !string.IsNullOrEmpty(scene.path)) {
                    open.Add(scene.path);
                }
            }

            return open;
        }

        /// <summary>Forwards operation messages to the Unity Console, prefixed with the scene name.</summary>
        private sealed class ConsoleLog : ISceneOperationLog {
            private readonly string scenePrefix;

            public ConsoleLog(string sceneName) {
                scenePrefix = $"[{sceneName}] ";
            }

            public void Info(string message, Object context = null) {
                Debug.Log(scenePrefix + message, context);
            }

            public void Error(string message, Object context = null) {
                Debug.LogError(scenePrefix + message, context);
            }
        }
    }
}
#endif
