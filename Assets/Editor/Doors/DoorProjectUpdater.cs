#if UNITY_EDITOR
using Doors;
using UnityEditor;
using UnityEditor.SceneManagement;

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
            for (var i = 0; i < EditorSceneManager.sceneCount; i++) {
                var scene = EditorSceneManager.GetSceneAt(i);
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
            for (var i = 0; i < prefabGuids.Length; i++) {
                var path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (string.IsNullOrWhiteSpace(path)) {
                    continue;
                }

                var root = PrefabUtility.LoadPrefabContents(path);
                try {
                    var doors = root.GetComponentsInChildren<Door>(true);
                    var prefabChanged = 0;

                    for (var j = 0; j < doors.Length; j++) {
                        var door = doors[j];
                        if (door == null) {
                            continue;
                        }

                        var so = new SerializedObject(door);

                        var linkProp = so.FindProperty("link");
                        if (linkProp == null) {
                            continue;
                        }

                        var sceneProp = linkProp.FindPropertyRelative("targetScene");
                        var guidProp = sceneProp != null ? sceneProp.FindPropertyRelative("sceneGuid") : null;
                        var idProp = linkProp.FindPropertyRelative("targetDoorId");

                        if (guidProp == null || idProp == null) {
                            continue;
                        }

                        if (guidProp.stringValue == targetSceneGuid && idProp.stringValue == oldDoorId) {
                            so.Update();
                            idProp.stringValue = newDoorId;
                            so.ApplyModifiedPropertiesWithoutUndo();
                            prefabChanged++;
                        }
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

        private static int ReplaceInScene(UnityEngine.SceneManagement.Scene scene, string targetSceneGuid,
            string oldDoorId, string newDoorId) {
            var changed = 0;
            var doors = DoorUtils.GetDoorsInScene(scene);

            for (var i = 0; i < doors.Count; i++) {
                var door = doors[i];
                if (door == null) {
                    continue;
                }

                var so = new SerializedObject(door);

                var linkProp = so.FindProperty("link");
                if (linkProp == null) {
                    continue;
                }

                var sceneProp = linkProp.FindPropertyRelative("targetScene");
                var guidProp = sceneProp != null ? sceneProp.FindPropertyRelative("sceneGuid") : null;
                var idProp = linkProp.FindPropertyRelative("targetDoorId");

                if (guidProp == null || idProp == null) {
                    continue;
                }

                if (guidProp.stringValue == targetSceneGuid && idProp.stringValue == oldDoorId) {
                    so.Update();
                    idProp.stringValue = newDoorId;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(door);
                    changed++;
                }
            }

            return changed;
        }
    }
}
#endif
