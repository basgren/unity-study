#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game.Doors.Editor;
using UnityEditor;

namespace Game.Features.Doors.Editor {
    /// <summary>
    /// Editor-only cache for listing entrance IDs inside a scene.
    /// Used by inspectors to show a dropdown of available target entrances.
    /// </summary>
    public static class SceneEntranceCache {
        /// <summary>
        /// Dropdown entry describing an entrance in a scene.
        /// </summary>
        public readonly struct EntranceInfo {
            public readonly string EntranceId;
            public readonly string Label;

            public EntranceInfo(string entranceId, string label) {
                EntranceId = entranceId;
                Label = label;
            }
        }

        private struct CacheEntry {
            public double time;
            public EntranceInfo[] entrances;
        }

        private static readonly Dictionary<string, CacheEntry> cache = new Dictionary<string, CacheEntry>();
        private const double CacheTtlSeconds = 2.0;

        /// <summary>
        /// Returns a cached flat list of entrances for the specified scene GUID.
        /// Labels are formatted as: "{EntranceId} ({GameObjectName})".
        /// </summary>
        public static EntranceInfo[] GetEntrancesByGuid(string sceneGuid) {
            if (string.IsNullOrWhiteSpace(sceneGuid)) {
                return Array.Empty<EntranceInfo>();
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode) {
                return Array.Empty<EntranceInfo>();
            }

            if (cache.TryGetValue(sceneGuid, out var entry)) {
                if (EditorApplication.timeSinceStartup - entry.time < CacheTtlSeconds) {
                    return entry.entrances;
                }
            }

            var entrances = LoadEntrancesFromSceneGuid(sceneGuid);
            cache[sceneGuid] = new CacheEntry { time = EditorApplication.timeSinceStartup, entrances = entrances };
            return entrances;
        }

        /// <summary>
        /// Clears all cached scene entrance lists.
        /// </summary>
        public static void InvalidateAll() {
            cache.Clear();
        }

        private static EntranceInfo[] LoadEntrancesFromSceneGuid(string sceneGuid) {
            var path = AssetDatabase.GUIDToAssetPath(sceneGuid);
            if (string.IsNullOrWhiteSpace(path)) {
                return Array.Empty<EntranceInfo>();
            }

            var list = new List<EntranceInfo>();

            DoorEditorUtils.ExecuteInScene(path, scene => {
                var entrances = EntranceUtils.GetEntrancesInScene(scene);
                for (var i = 0; i < entrances.Count; i++) {
                    var entrance = entrances[i];
                    if (entrance == null) {
                        continue;
                    }

                    var id = entrance.EntranceId;
                    var labelId = string.IsNullOrWhiteSpace(id) ? "<empty>" : id;

                    var objName = entrance.gameObject != null ? entrance.gameObject.name : "<null>";
                    var label = $"{labelId} ({objName})";

                    list.Add(new EntranceInfo(id, label));
                }
            });

            list.Sort((a, b) => string.CompareOrdinal(a.EntranceId, b.EntranceId));
            return list.ToArray();
        }
    }
}
#endif
