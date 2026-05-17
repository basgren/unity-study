#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Game.Features.Portals.Common.Editor {
    /// <summary>
    /// Editor-only helper that rewrites every PortalLink whose target matches an old id, replacing
    /// it with a new id. Driven by a <see cref="PortalKind"/> so the same code handles every portal
    /// kind.
    /// - Open scenes are only marked dirty (no auto-save).
    /// - Prefabs are saved automatically.
    /// </summary>
    public static class PortalProjectUpdater {
        public static void ReplaceReferencesInOpenScenes(PortalKind kind, string targetSceneGuid,
            string oldId, string newId, ref int changedLinks) {
            for (var i = 0; i < SceneManager.sceneCount; i++) {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded) {
                    continue;
                }

                var sceneChanged = ReplaceInScene(kind, scene, targetSceneGuid, oldId, newId);
                if (sceneChanged > 0) {
                    changedLinks += sceneChanged;
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }
        }

        public static void ReplaceReferencesInAllPrefabs(PortalKind kind, string targetSceneGuid,
            string oldId, string newId, ref int changedLinks) {
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var prefabGuid in prefabGuids) {
                var path = AssetDatabase.GUIDToAssetPath(prefabGuid);
                if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/")) {
                    continue;
                }

                var root = PrefabUtility.LoadPrefabContents(path);
                try {
                    var components = root.GetComponentsInChildren(kind.ComponentType, true);
                    var prefabChanged = 0;

                    foreach (var comp in components) {
                        if (comp == null) {
                            continue;
                        }

                        if (TryReplaceLink(comp, targetSceneGuid, oldId, newId)) {
                            prefabChanged++;
                        }
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

        public static void ReplaceReferencesInAllScenesOnDisk(PortalKind kind, string targetSceneGuid,
            string oldId, string newId, ref int changedLinks, ref int changedScenes) {
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
                    if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/")) {
                        continue;
                    }

                    var title = $"Updating {kind.KindName} references in project scenes";
                    var info = $"Scene {i + 1}/{total}\n{path}";
                    var progress = total > 0 ? (float)i / total : 1f;
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
                        var sceneChanged = ReplaceInScene(kind, scene, targetSceneGuid, oldId, newId);
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

        private static int ReplaceInScene(PortalKind kind, Scene scene, string targetSceneGuid,
            string oldId, string newId) {
            var changed = 0;
            foreach (var portal in kind.GetPortalsInScene(scene)) {
                if (portal == null) {
                    continue;
                }

                var comp = portal as UnityEngine.Object;
                if (comp == null) {
                    continue;
                }

                if (TryReplaceLink(comp, targetSceneGuid, oldId, newId)) {
                    EditorUtility.SetDirty(comp);
                    changed++;
                }
            }

            return changed;
        }

        private static bool TryReplaceLink(UnityEngine.Object obj, string targetSceneGuid,
            string oldId, string newId) {
            var so = new SerializedObject(obj);
            var linkProp = so.FindProperty("link");
            if (linkProp == null) {
                return false;
            }

            var sceneProp = linkProp.FindPropertyRelative("targetScene");
            var guidProp = sceneProp?.FindPropertyRelative("sceneGuid");
            var idProp = linkProp.FindPropertyRelative("targetId");
            if (guidProp == null || idProp == null) {
                return false;
            }

            if (guidProp.stringValue != targetSceneGuid || idProp.stringValue != oldId) {
                return false;
            }

            so.Update();
            idProp.stringValue = newId;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}
#endif
