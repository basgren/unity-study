#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Doors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Editor.Doors {
    /// <summary>
    /// Editor-only cache for listing door IDs inside a scene.
    /// Used by inspectors to show a dropdown of available target doors.
    /// </summary>
    public static class SceneDoorCache {
        /// <summary>
        /// Dropdown entry describing a door in a scene.
        /// </summary>
        public readonly struct DoorInfo {
            public readonly string DoorId;
            public readonly string Label;

            public DoorInfo(string doorId, string label) {
                DoorId = doorId;
                Label = label;
            }
        }

        private struct CacheEntry {
            public double time;
            public DoorInfo[] doors;
        }

        private static readonly Dictionary<string, CacheEntry> cache = new Dictionary<string, CacheEntry>();
        private const double CacheTtlSeconds = 2.0;

        /// <summary>
        /// Returns a cached flat list of doors for the specified scene GUID.
        /// Labels are formatted as: "{DoorId} ({GameObjectName})".
        /// </summary>
        public static DoorInfo[] GetDoorsByGuid(string sceneGuid) {
            if (string.IsNullOrWhiteSpace(sceneGuid)) {
                return Array.Empty<DoorInfo>();
            }
            
            if (EditorApplication.isPlayingOrWillChangePlaymode) {
                return Array.Empty<DoorInfo>();
            }

            if (cache.TryGetValue(sceneGuid, out var entry)) {
                if (EditorApplication.timeSinceStartup - entry.time < CacheTtlSeconds) {
                    return entry.doors;
                }
            }

            var doors = LoadDoorsFromSceneGuid(sceneGuid);
            cache[sceneGuid] = new CacheEntry { time = EditorApplication.timeSinceStartup, doors = doors };
            return doors;
        }

        /// <summary>
        /// Clears all cached scene door lists.
        /// </summary>
        public static void InvalidateAll() {
            cache.Clear();
        }

        private static DoorInfo[] LoadDoorsFromSceneGuid(string sceneGuid) {
            var path = AssetDatabase.GUIDToAssetPath(sceneGuid);
            if (string.IsNullOrWhiteSpace(path)) {
                return Array.Empty<DoorInfo>();
            }

            // Avoid closing a scene that was already loaded by the user/editor.
            var already = SceneManager.GetSceneByPath(path);
            var alreadyLoaded = already.IsValid() && already.isLoaded;

            var scene = alreadyLoaded ? already : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try {
                var list = new List<DoorInfo>();

                var roots = scene.GetRootGameObjects();
                for (var i = 0; i < roots.Length; i++) {
                    var doors = roots[i].GetComponentsInChildren<Door>(true);
                    for (var j = 0; j < doors.Length; j++) {
                        var door = doors[j];
                        if (door == null) {
                            continue;
                        }

                        var id = door.DoorId;
                        var labelId = string.IsNullOrWhiteSpace(id) ? "<empty>" : id;

                        // Flat list as requested: ID (ObjectName)
                        var objName = door.gameObject != null ? door.gameObject.name : "<null>";
                        var label = $"{labelId} ({objName})";

                        list.Add(new DoorInfo(id, label));
                    }
                }

                list.Sort((a, b) => string.CompareOrdinal(a.DoorId, b.DoorId));
                return list.ToArray();
            }
            finally {
                if (!alreadyLoaded) {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }
}
#endif
