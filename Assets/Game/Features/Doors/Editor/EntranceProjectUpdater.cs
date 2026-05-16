#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Game.Features.Doors.Editor {
    /// <summary>
    /// Editor-only helper that updates EntranceLink references after an EntranceId rename.
    /// Important:
    /// - Open scenes are only marked dirty (no auto-save).
    /// - Prefabs are saved automatically.
    /// </summary>
    public static class EntranceProjectUpdater {
        /// <summary>
        /// Updates references in currently open scenes only.
        /// Scenes are marked dirty when modified.
        /// </summary>
        public static void ReplaceReferencesInOpenScenes(string targetSceneGuid, string oldEntranceId,
            string newEntranceId, ref int changedLinks) {
            for (var i = 0; i < SceneManager.sceneCount; i++) {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded) {
                    continue;
                }

                var sceneChanged = ReplaceInScene(scene, targetSceneGuid, oldEntranceId, newEntranceId);
                if (sceneChanged > 0) {
                    changedLinks += sceneChanged;
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }
        }

        /// <summary>
        /// Updates references in all prefabs in the project.
        /// Modified prefabs are saved automatically.
        /// </summary>
        public static void ReplaceReferencesInAllPrefabs(string targetSceneGuid, string oldEntranceId,
            string newEntranceId, ref int changedLinks) {
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var prefabGuid in prefabGuids) {
                var path = AssetDatabase.GUIDToAssetPath(prefabGuid);
                if (string.IsNullOrWhiteSpace(path)) {
                    continue;
                }

                var root = PrefabUtility.LoadPrefabContents(path);
                try {
                    var entrances = root.GetComponentsInChildren<Entrance>(true);
                    var prefabChanged = 0;

                    foreach (var entrance in entrances) {
                        if (entrance == null) {
                            continue;
                        }

                        var so = new SerializedObject(entrance);

                        var linkProp = so.FindProperty("link");
                        if (linkProp == null) {
                            continue;
                        }

                        var sceneProp = linkProp.FindPropertyRelative("targetScene");
                        var guidProp = sceneProp?.FindPropertyRelative("sceneGuid");
                        var idProp = linkProp.FindPropertyRelative("targetEntranceId");

                        if (
                            guidProp == null
                            || guidProp.stringValue != targetSceneGuid
                            || idProp == null
                            || idProp.stringValue != oldEntranceId
                        ) {
                            continue;
                        }

                        so.Update();
                        idProp.stringValue = newEntranceId;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        prefabChanged++;
                    }

                    if (prefabChanged > 0) {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        changedLinks += prefabChanged;
                    }
                } finally {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
        }

        private static int ReplaceInScene(
            Scene scene,
            string targetSceneGuid,
            string oldEntranceId,
            string newEntranceId
        ) {
            var changed = 0;
            var entrances = EntranceUtils.GetEntrancesInScene(scene);

            foreach (var entrance in entrances) {
                if (entrance == null) {
                    continue;
                }

                var so = new SerializedObject(entrance);

                var linkProp = so.FindProperty("link");
                if (linkProp == null) {
                    continue;
                }

                var sceneProp = linkProp.FindPropertyRelative("targetScene");
                var guidProp = sceneProp?.FindPropertyRelative("sceneGuid");
                var idProp = linkProp.FindPropertyRelative("targetEntranceId");

                if (
                    guidProp == null
                    || guidProp.stringValue != targetSceneGuid
                    || idProp == null
                    || idProp.stringValue != oldEntranceId
                ) {
                    continue;
                }

                so.Update();
                idProp.stringValue = newEntranceId;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(entrance);
                changed++;
            }

            return changed;
        }

        /// <summary>
        /// Updates references in ALL scenes in the project (including scenes not in Build Settings).
        /// Scenes that are currently open in the editor are NOT auto-saved; they are expected to be handled
        /// by ReplaceReferencesInOpenScenes (mark dirty).
        /// All other scenes are opened additively, modified, saved, and closed.
        /// </summary>
        public static void ReplaceReferencesInAllScenesOnDisk(string targetSceneGuid, string oldEntranceId,
            string newEntranceId, ref int changedLinks, ref int changedScenes) {
            // Collect currently open scene paths to avoid saving them automatically.
            var openScenePaths = new HashSet<string>();

            for (var i = 0; i < SceneManager.sceneCount; i++) {
                var s = SceneManager.GetSceneAt(i);
                if (s.IsValid() && s.isLoaded && !string.IsNullOrWhiteSpace(s.path)) {
                    openScenePaths.Add(s.path);
                }
            }

            var sceneGuids = AssetDatabase.FindAssets("t:Scene");
            var total = sceneGuids.Length;

            try {
                for (var i = 0; i < total; i++) {
                    var path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                    if (string.IsNullOrWhiteSpace(path)) {
                        continue;
                    }

                    var title = "Updating Entrance references in project scenes";
                    var info = $"Scene {i + 1}/{total}\n{path}";
                    var progress = total > 0 ? (float)i / (float)total : 1f;

                    if (EditorUtility.DisplayCancelableProgressBar(title, info, progress)) {
                        break;
                    }

                    if (openScenePaths.Contains(path)) {
                        continue;
                    }

                    var already = SceneManager.GetSceneByPath(path);
                    var alreadyLoaded = already.IsValid() && already.isLoaded;

                    var scene = alreadyLoaded ? already : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    try {
                        var sceneChanged = ReplaceInScene(scene, targetSceneGuid, oldEntranceId, newEntranceId);
                        if (sceneChanged > 0) {
                            changedLinks += sceneChanged;
                            changedScenes++;

                            EditorSceneManager.MarkSceneDirty(scene);
                            EditorSceneManager.SaveScene(scene);
                        }
                    } finally {
                        if (!alreadyLoaded) {
                            EditorSceneManager.CloseScene(scene, true);
                        }
                    }
                }
            } finally {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
#endif
