#if UNITY_EDITOR
using System;
using Game.Doors.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Features.Doors.Editor {
    /// <summary>
    /// Repairs cached SceneReference.scenePath for DoorLink target scenes.
    /// Uses SceneReference.sceneGuid as the source of truth.
    /// </summary>
    public static class DoorSceneReferenceRepair {
        [MenuItem("Tools/Doors/Repair Door Scene References")]
        public static void RepairMenu() {
            RepairAllDoorSceneReferences(showProgressBar: true);
            EditorUtility.DisplayDialog("Doors", "Repair finished. Check Console for details.", "OK");
        }

        public static void RepairAllDoorSceneReferences(bool showProgressBar) {
            var fixedLinks = 0;
            var fixedScenes = 0;
            var fixedPrefabs = 0;

            try {
                // Scenes
                var sceneGuids = AssetDatabase.FindAssets("t:Scene");
                for (var i = 0; i < sceneGuids.Length; i++) {
                    var path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                    if (string.IsNullOrWhiteSpace(path)) {
                        continue;
                    }

                    if (showProgressBar) {
                        var title = "Repairing Door Scene References";
                        var info = $"Scene {i + 1}/{sceneGuids.Length}\n{path}";
                        var progress = (float)i / Mathf.Max(1, sceneGuids.Length);

                        if (EditorUtility.DisplayCancelableProgressBar(title, info, progress)) {
                            break;
                        }
                    }

                    var already = SceneManager.GetSceneByPath(path);
                    var alreadyLoaded = already.IsValid() && already.isLoaded;

                    var scene = alreadyLoaded ? already : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    try {
                        var changedInScene = RepairInScene(scene);
                        if (changedInScene > 0) {
                            fixedLinks += changedInScene;
                            fixedScenes++;

                            EditorSceneManager.MarkSceneDirty(scene);
                            // This tool is explicitly a "repair" action -> it is OK to save.
                            EditorSceneManager.SaveScene(scene);
                        }
                    } finally {
                        if (!alreadyLoaded) {
                            EditorSceneManager.CloseScene(scene, true);
                        }
                    }
                }

                // Prefabs
                var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
                for (var i = 0; i < prefabGuids.Length; i++) {
                    var prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                    if (string.IsNullOrWhiteSpace(prefabPath)) {
                        continue;
                    }

                    if (showProgressBar) {
                        var title = "Repairing Door Scene References";
                        var info = $"Prefab {i + 1}/{prefabGuids.Length}\n{prefabPath}";
                        var progress = (float)i / Mathf.Max(1, prefabGuids.Length);

                        if (EditorUtility.DisplayCancelableProgressBar(title, info, progress)) {
                            break;
                        }
                    }

                    var root = PrefabUtility.LoadPrefabContents(prefabPath);
                    try {
                        var changedInPrefab = RepairInPrefabRoot(root);
                        if (changedInPrefab > 0) {
                            fixedLinks += changedInPrefab;
                            fixedPrefabs++;
                            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                        }
                    } finally {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            } finally {
                if (showProgressBar) {
                    EditorUtility.ClearProgressBar();
                }
            }

            AssetDatabase.SaveAssets();

            Debug.Log($"Doors repair complete. Fixed links: {fixedLinks}, scenes saved: {fixedScenes}, prefabs saved: {fixedPrefabs}.");
        }

        private static int RepairInScene(Scene scene) {
            var changed = 0;
            var roots = scene.GetRootGameObjects();

            for (var i = 0; i < roots.Length; i++) {
                var doors = roots[i].GetComponentsInChildren<Door>(true);
                for (var j = 0; j < doors.Length; j++) {
                    changed += RepairDoorSerialized(doors[j]);
                }
            }

            return changed;
        }

        private static int RepairInPrefabRoot(GameObject root) {
            var changed = 0;
            var doors = root.GetComponentsInChildren<Door>(true);

            for (var i = 0; i < doors.Length; i++) {
                changed += RepairDoorSerialized(doors[i]);
            }

            return changed;
        }

        private static int RepairDoorSerialized(Door door) {
            if (door == null) {
                return 0;
            }

            var so = new SerializedObject(door);
            var linkProp = so.FindProperty("link");
            if (linkProp == null) {
                return 0;
            }

            var sceneProp = linkProp.FindPropertyRelative("targetScene");
            if (sceneProp == null) {
                return 0;
            }

            var guidProp = sceneProp.FindPropertyRelative("sceneGuid");
            var pathProp = sceneProp.FindPropertyRelative("scenePath");

            if (guidProp == null || pathProp == null) {
                return 0;
            }

            var guid = guidProp.stringValue;
            if (string.IsNullOrWhiteSpace(guid)) {
                return 0;
            }

            var actualPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(actualPath)) {
                return 0;
            }

            if (string.Equals(pathProp.stringValue, actualPath, StringComparison.Ordinal)) {
                return 0;
            }

            so.Update();
            pathProp.stringValue = actualPath;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(door);

            SceneDoorCache.InvalidateAll();
            return 1;
        }
    }
}
#endif
