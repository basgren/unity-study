#if UNITY_EDITOR
using System.Collections.Generic;
using Doors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Editor.Doors {
    /// <summary>
    /// Editor-only helper that updates DoorLink references after a DoorId rename.
    /// Important:
    /// - Open scenes are only marked dirty (no auto-save).
    /// - Prefabs are saved automatically.
    /// </summary>
    public static class DoorProjectUpdater {
        /// <summary>
        /// Updates references in currently open scenes only.
        /// Scenes are marked dirty when modified.
        /// </summary>
        public static void ReplaceReferencesInOpenScenes(string targetSceneGuid, string oldDoorId, string newDoorId,
            ref int changedLinks) {
            for (var i = 0; i < SceneManager.sceneCount; i++) {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded) {
                    continue;
                }

                var sceneChanged = ReplaceInScene(scene, targetSceneGuid, oldDoorId, newDoorId);
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
        public static void ReplaceReferencesInAllPrefabs(string targetSceneGuid, string oldDoorId, string newDoorId,
            ref int changedLinks) {
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var prefabGuid in prefabGuids) {
                var path = AssetDatabase.GUIDToAssetPath(prefabGuid);
                if (string.IsNullOrWhiteSpace(path)) {
                    continue;
                }

                var root = PrefabUtility.LoadPrefabContents(path);
                try {
                    var doors = root.GetComponentsInChildren<Door>(true);
                    var prefabChanged = 0;

                    foreach (var door in doors) {
                        if (door == null) {
                            continue;
                        }

                        var so = new SerializedObject(door);

                        var linkProp = so.FindProperty("link");
                        if (linkProp == null) {
                            continue;
                        }

                        var sceneProp = linkProp.FindPropertyRelative("targetScene");
                        var guidProp = sceneProp?.FindPropertyRelative("sceneGuid");
                        var idProp = linkProp.FindPropertyRelative("targetDoorId");

                        if (
                            guidProp == null
                            || guidProp.stringValue != targetSceneGuid
                            || idProp == null
                            || idProp.stringValue != oldDoorId
                        ) {
                            continue;
                        }

                        so.Update();
                        idProp.stringValue = newDoorId;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        prefabChanged++;
                    }

                    if (prefabChanged > 0) {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        changedLinks += prefabChanged;
                    }
                }
                finally {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
        }

        private static int ReplaceInScene(
            Scene scene,
            string targetSceneGuid,
            string oldDoorId,
            string newDoorId
        ) {
            var changed = 0;
            var doors = DoorUtils.GetDoorsInScene(scene);

            foreach (var door in doors) {
                if (door == null) {
                    continue;
                }

                var so = new SerializedObject(door);

                var linkProp = so.FindProperty("link");
                if (linkProp == null) {
                    continue;
                }

                var sceneProp = linkProp.FindPropertyRelative("targetScene");
                var guidProp = sceneProp?.FindPropertyRelative("sceneGuid");
                var idProp = linkProp.FindPropertyRelative("targetDoorId");

                if (
                    guidProp == null
                    || guidProp.stringValue != targetSceneGuid
                    || idProp == null
                    || idProp.stringValue != oldDoorId
                ) {
                    continue;
                }

                so.Update();
                idProp.stringValue = newDoorId;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(door);
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
        public static void ReplaceReferencesInAllScenesOnDisk(string targetSceneGuid, string oldDoorId,
            string newDoorId, ref int changedLinks, ref int changedScenes) {
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

                    // Progress + cancel.
                    var title = "Updating Door references in project scenes";
                    var info = $"Scene {i + 1}/{total}\n{path}";
                    var progress = total > 0 ? (float)i / (float)total : 1f;

                    if (EditorUtility.DisplayCancelableProgressBar(title, info, progress)) {
                        // User cancelled. Note: some scenes may have already been updated/saved.
                        break;
                    }

                    // Do not auto-save scenes that are currently open.
                    if (openScenePaths.Contains(path)) {
                        continue;
                    }

                    // Avoid touching scenes already loaded (should be rare, but safe).
                    var already = SceneManager.GetSceneByPath(path);
                    var alreadyLoaded = already.IsValid() && already.isLoaded;

                    var scene = alreadyLoaded ? already : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    try {
                        var sceneChanged = ReplaceInScene(scene, targetSceneGuid, oldDoorId, newDoorId);
                        if (sceneChanged > 0) {
                            changedLinks += sceneChanged;
                            changedScenes++;

                            EditorSceneManager.MarkSceneDirty(scene);
                            EditorSceneManager.SaveScene(scene);
                        }
                    }
                    finally {
                        if (!alreadyLoaded) {
                            EditorSceneManager.CloseScene(scene, true);
                        }
                    }
                }
            }
            finally {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
#endif
