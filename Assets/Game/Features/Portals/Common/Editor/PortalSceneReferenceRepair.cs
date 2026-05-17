#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Features.Portals.Common.Editor {
    /// <summary>
    /// Repairs cached SceneReference.scenePath for portal links across the project.
    /// Uses SceneReference.sceneGuid as the source of truth. Works for any component type
    /// that has a serialized "link" field containing a SceneReference under "targetScene".
    /// Each portal kind registers itself via <see cref="RegisterPortalType"/>.
    /// </summary>
    public static class PortalSceneReferenceRepair {
        private static readonly List<Type> RegisteredPortalTypes = new List<Type>();

        /// <summary>
        /// Registers a portal component type to be processed by the repair tool. Idempotent.
        /// </summary>
        public static void RegisterPortalType(Type componentType) {
            if (componentType == null) {
                return;
            }

            if (!RegisteredPortalTypes.Contains(componentType)) {
                RegisteredPortalTypes.Add(componentType);
            }
        }

        [MenuItem("Tools/Portals/Repair Scene References")]
        public static void RepairMenu() {
            RepairAllPortalSceneReferences(showProgressBar: true);
            EditorUtility.DisplayDialog("Portals", "Repair finished. Check Console for details.", "OK");
        }

        public static void RepairAllPortalSceneReferences(bool showProgressBar) {
            var fixedLinks = 0;
            var fixedScenes = 0;
            var fixedPrefabs = 0;

            try {
                var sceneGuids = AssetDatabase.FindAssets("t:Scene");
                for (var i = 0; i < sceneGuids.Length; i++) {
                    var path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                    if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/")) {
                        continue;
                    }

                    if (showProgressBar) {
                        var title = "Repairing Portal Scene References";
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

                var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
                for (var i = 0; i < prefabGuids.Length; i++) {
                    var prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                    if (string.IsNullOrWhiteSpace(prefabPath) || !prefabPath.StartsWith("Assets/")) {
                        continue;
                    }

                    if (showProgressBar) {
                        var title = "Repairing Portal Scene References";
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

            if (fixedLinks > 0) {
                ScenePortalCacheInvalidator.InvalidateAll();
            }

            Debug.Log($"Portals repair complete. Fixed links: {fixedLinks}, scenes saved: {fixedScenes}, prefabs saved: {fixedPrefabs}.");
        }

        private static int RepairInScene(Scene scene) {
            var changed = 0;
            var roots = scene.GetRootGameObjects();

            for (var i = 0; i < roots.Length; i++) {
                for (var t = 0; t < RegisteredPortalTypes.Count; t++) {
                    var components = roots[i].GetComponentsInChildren(RegisteredPortalTypes[t], true);
                    for (var j = 0; j < components.Length; j++) {
                        changed += RepairComponentSerialized(components[j]);
                    }
                }
            }

            return changed;
        }

        private static int RepairInPrefabRoot(GameObject root) {
            var changed = 0;

            for (var t = 0; t < RegisteredPortalTypes.Count; t++) {
                var components = root.GetComponentsInChildren(RegisteredPortalTypes[t], true);
                for (var i = 0; i < components.Length; i++) {
                    changed += RepairComponentSerialized(components[i]);
                }
            }

            return changed;
        }

        private static int RepairComponentSerialized(Component component) {
            if (component == null) {
                return 0;
            }

            var so = new SerializedObject(component);
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
            EditorUtility.SetDirty(component);
            return 1;
        }
    }
}
#endif
