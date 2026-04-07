#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Game.Core.Services.SceneState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Game.Editor.SceneState {
    /// <summary>
    /// Assigns stable Save IDs to StateRoot components that have none.
    /// Runs automatically when any scene is saved. Existing IDs are never changed.
    /// IDs have the form "{PrefabName}_{N}" (e.g. "Chest_1") where N is a counter
    /// unique within the scene. Non-prefab objects use the GameObject name as prefix.
    /// </summary>
    [InitializeOnLoad]
    public static class StateRootIdAssigner {
        static StateRootIdAssigner() {
            EditorSceneManager.sceneSaving += OnSceneSaving;
        }

        private static void OnSceneSaving(Scene scene, string path) {
            var needsId = CollectRootsWithoutId(scene);
            if (needsId.Count == 0) {
                return;
            }

            int nextId = FindNextAvailableId(scene);

            foreach (var root in needsId) {
                var prefix = GetPrefix(root);
                var so = new SerializedObject(root);
                so.FindProperty("saveId").stringValue = $"{prefix}_{nextId}";
                so.ApplyModifiedProperties();
                nextId++;
            }
        }

        private static string GetPrefix(StateRoot root) {
            var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root.gameObject);
            if (!string.IsNullOrEmpty(prefabPath)) {
                return Path.GetFileNameWithoutExtension(prefabPath);
            }

            return root.gameObject.name;
        }

        private static List<StateRoot> CollectRootsWithoutId(Scene scene) {
            var result = new List<StateRoot>();
            foreach (var go in scene.GetRootGameObjects()) {
                foreach (var root in go.GetComponentsInChildren<StateRoot>(true)) {
                    if (!root.SkipSave && string.IsNullOrEmpty(root.SaveId)) {
                        result.Add(root);
                    }
                }
            }

            return result;
        }

        private static int FindNextAvailableId(Scene scene) {
            int max = 0;
            foreach (var go in scene.GetRootGameObjects()) {
                foreach (var root in go.GetComponentsInChildren<StateRoot>(true)) {
                    var id = root.SaveId;
                    // Extract the trailing number from ids like "Chest_3" or plain "3".
                    var lastUnderscore = id.LastIndexOf('_');
                    var numPart = lastUnderscore >= 0 ? id.Substring(lastUnderscore + 1) : id;
                    if (int.TryParse(numPart, out var n) && n > max) {
                        max = n;
                    }
                }
            }

            return max + 1;
        }
    }
}
#endif
