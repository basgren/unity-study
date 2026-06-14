#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Game.Core.Services.SceneState;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor.SceneState {
    /// <summary>
    /// Assigns stable Save IDs to StateRoot components that have none.
    /// Runs automatically when any scene is saved. Existing IDs are never changed.
    /// IDs have the form "{PrefabName}_{N}" (e.g. "Chest_1") where N is a counter
    /// unique within the scene. Non-prefab objects use the GameObject name as prefix.
    /// After assigning, it logs an error for any duplicate saveId in the scene (e.g. from
    /// duplicating a GameObject) so the collision can be repaired manually.
    /// </summary>
    [InitializeOnLoad]
    public static class StateRootIdAssigner {
        static StateRootIdAssigner() {
            EditorSceneManager.sceneSaving += OnSceneSaving;
        }

        private static void OnSceneSaving(Scene scene, string path) {
            AssignMissingIds(scene);
            // Existing ids are never auto-changed (a duplicate would otherwise self-heal and mask the
            // mistake). Duplicating a GameObject copies its saveId, so flag collisions for manual repair:
            // clear one of the offenders' saveId, then save again to get a fresh id.
            LogDuplicateIds(scene);
        }

        private static void AssignMissingIds(Scene scene) {
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

        private static void LogDuplicateIds(Scene scene) {
            var errors = StateRootValidator.ValidateScene(scene);
            foreach (var error in errors) {
                Debug.LogError(error.Message, error.Context);
            }
        }

        /// <summary>
        /// Reassigns a fresh unique id to every duplicate saveId in the scene; the first occurrence
        /// of each id keeps it. Empty ids are left to the normal on-save assignment. Returns the
        /// number of ids changed. Does NOT save the scene — the caller marks it dirty and saves.
        /// </summary>
        public static int ReassignDuplicateIds(Scene scene) {
            if (!scene.IsValid()) {
                return 0;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var duplicates = new List<StateRoot>();

            foreach (var go in scene.GetRootGameObjects()) {
                foreach (var root in go.GetComponentsInChildren<StateRoot>(true)) {
                    if (root.SkipSave || string.IsNullOrEmpty(root.SaveId)) {
                        continue;
                    }

                    // First time we see an id it is the keeper; any later root with the same id is a duplicate.
                    if (!seen.Add(root.SaveId)) {
                        duplicates.Add(root);
                    }
                }
            }

            if (duplicates.Count == 0) {
                return 0;
            }

            int nextId = FindNextAvailableId(scene);
            foreach (var root in duplicates) {
                var prefix = GetPrefix(root);
                var so = new SerializedObject(root);
                so.FindProperty("saveId").stringValue = $"{prefix}_{nextId}";
                so.ApplyModifiedProperties();
                nextId++;
            }

            return duplicates.Count;
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
