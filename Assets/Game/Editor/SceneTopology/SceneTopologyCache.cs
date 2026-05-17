using System;
using System.Collections.Generic;
using System.IO;
using Game.Editor.SceneTopology.Model;
using UnityEngine;

namespace Game.Editor.SceneTopology {
    /// <summary>
    /// On-disk cache of per-scene topology snapshots, keyed by scene GUID and validated against
    /// <c>AssetDatabase.GetAssetDependencyHash</c>. Lives under <c>Library/</c> so it's gitignored and
    /// gets cleared by Unity when the project library is rebuilt.
    /// Bump <see cref="CurrentSchemaVersion"/> whenever portal scanning or stored fields change so
    /// stale entries are discarded automatically.
    /// </summary>
    internal static class SceneTopologyCache {
        private const string CachePath = "Library/SceneTopologyCache.json";
        private const int CurrentSchemaVersion = 1;

        public sealed class Entry {
            public string DependencyHash;
            public SceneNodeData Node;
        }

        public static Dictionary<string, Entry> Load() {
            var result = new Dictionary<string, Entry>();
            if (!File.Exists(CachePath)) {
                return result;
            }

            try {
                var json = File.ReadAllText(CachePath);
                var file = JsonUtility.FromJson<CacheFile>(json);
                if (file == null || file.SchemaVersion != CurrentSchemaVersion || file.Entries == null) {
                    return result;
                }

                for (var i = 0; i < file.Entries.Count; i++) {
                    var dto = file.Entries[i];
                    if (string.IsNullOrEmpty(dto.SceneGuid)) {
                        continue;
                    }

                    result[dto.SceneGuid] = new Entry {
                        DependencyHash = dto.DependencyHash,
                        Node = ToNode(dto),
                    };
                }
            } catch (Exception ex) {
                // Corrupt cache should never break a scan — just discard and rebuild.
                Debug.LogWarning("[SceneTopology] Failed to read cache, ignoring: " + ex.Message);
            }

            return result;
        }

        public static void Save(Dictionary<string, Entry> entries) {
            var file = new CacheFile { SchemaVersion = CurrentSchemaVersion };
            foreach (var kvp in entries) {
                file.Entries.Add(ToDto(kvp.Key, kvp.Value));
            }

            try {
                var dir = Path.GetDirectoryName(CachePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(CachePath, JsonUtility.ToJson(file, prettyPrint: false));
            } catch (Exception ex) {
                Debug.LogWarning("[SceneTopology] Failed to write cache: " + ex.Message);
            }
        }

        private static SceneNodeData ToNode(CachedSceneDto dto) {
            var node = new SceneNodeData {
                SceneGuid = dto.SceneGuid,
                ScenePath = dto.ScenePath,
                DisplayName = dto.DisplayName,
                RegionKey = dto.RegionKey,
                BoundsLocal = new Rect(dto.BoundsX, dto.BoundsY, dto.BoundsW, dto.BoundsH),
                BoundsAreFallback = dto.BoundsAreFallback,
            };

            if (dto.Portals == null) {
                return node;
            }

            for (var i = 0; i < dto.Portals.Count; i++) {
                var p = dto.Portals[i];
                node.Portals.Add(new PortalData {
                    Id = p.Id,
                    Kind = (PortalKindRef)p.Kind,
                    LocalPos = new Vector2(p.LocalX, p.LocalY),
                    TargetSceneGuid = p.TargetSceneGuid,
                    TargetId = p.TargetId,
                });
            }

            return node;
        }

        private static CachedSceneDto ToDto(string sceneGuid, Entry entry) {
            var node = entry.Node;
            var dto = new CachedSceneDto {
                SceneGuid = sceneGuid,
                ScenePath = node.ScenePath,
                DependencyHash = entry.DependencyHash,
                DisplayName = node.DisplayName,
                RegionKey = node.RegionKey,
                BoundsX = node.BoundsLocal.x,
                BoundsY = node.BoundsLocal.y,
                BoundsW = node.BoundsLocal.width,
                BoundsH = node.BoundsLocal.height,
                BoundsAreFallback = node.BoundsAreFallback,
            };

            for (var i = 0; i < node.Portals.Count; i++) {
                var p = node.Portals[i];
                dto.Portals.Add(new CachedPortalDto {
                    Id = p.Id,
                    Kind = (int)p.Kind,
                    LocalX = p.LocalPos.x,
                    LocalY = p.LocalPos.y,
                    TargetSceneGuid = p.TargetSceneGuid,
                    TargetId = p.TargetId,
                });
            }

            return dto;
        }

        // JsonUtility needs concrete, [Serializable], non-generic top-level types with public fields.
        [Serializable]
        private sealed class CacheFile {
            public int SchemaVersion;
            public List<CachedSceneDto> Entries = new List<CachedSceneDto>();
        }

        [Serializable]
        private sealed class CachedSceneDto {
            public string SceneGuid;
            public string ScenePath;
            public string DependencyHash;
            public string DisplayName;
            public string RegionKey;
            public float BoundsX;
            public float BoundsY;
            public float BoundsW;
            public float BoundsH;
            public bool BoundsAreFallback;
            public List<CachedPortalDto> Portals = new List<CachedPortalDto>();
        }

        [Serializable]
        private sealed class CachedPortalDto {
            public string Id;
            public int Kind;
            public float LocalX;
            public float LocalY;
            public string TargetSceneGuid;
            public string TargetId;
        }
    }
}
