#if UNITY_EDITOR
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
    }
}
#endif
