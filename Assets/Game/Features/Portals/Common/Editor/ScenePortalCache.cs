#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;

namespace Game.Features.Portals.Common.Editor {
    /// <summary>
    /// Per-kind cache of portal entries keyed by scene GUID. One instance lives on each
    /// <see cref="PortalKind"/>. Reads transparently open the scene via PortalEditorUtils and
    /// cache the result for a short TTL so inspectors stay snappy without re-scanning every frame.
    /// </summary>
    public sealed class ScenePortalCache {
        private const double CacheTtlSeconds = 2.0;
        private readonly PortalKind kind;
        private readonly Dictionary<string, CacheEntry> cache = new Dictionary<string, CacheEntry>();

        private struct CacheEntry {
            public double time;
            public PortalEntry[] entries;
        }

        public ScenePortalCache(PortalKind kind) {
            this.kind = kind;
            ScenePortalCacheInvalidator.OnInvalidate += InvalidateAll;
        }

        public PortalEntry[] GetEntriesByGuid(string sceneGuid) {
            if (string.IsNullOrWhiteSpace(sceneGuid)) {
                return Array.Empty<PortalEntry>();
            }

            // Cannot OpenScene during play-mode entry; return empty so the link drawer can
            // fall back to a read-only label instead of falsely warning "missing target".
            if (EditorApplication.isPlayingOrWillChangePlaymode) {
                return Array.Empty<PortalEntry>();
            }

            if (cache.TryGetValue(sceneGuid, out var entry)) {
                if (EditorApplication.timeSinceStartup - entry.time < CacheTtlSeconds) {
                    return entry.entries;
                }
            }

            var entries = Load(sceneGuid);
            cache[sceneGuid] = new CacheEntry { time = EditorApplication.timeSinceStartup, entries = entries };
            return entries;
        }

        public void InvalidateAll() {
            cache.Clear();
        }

        private PortalEntry[] Load(string sceneGuid) {
            var path = AssetDatabase.GUIDToAssetPath(sceneGuid);
            if (string.IsNullOrWhiteSpace(path)) {
                return Array.Empty<PortalEntry>();
            }

            var list = new List<PortalEntry>();
            PortalEditorUtils.ExecuteInScene(path, scene => {
                foreach (var portal in kind.GetPortalsInScene(scene)) {
                    if (portal == null) {
                        continue;
                    }

                    var go = portal.gameObject;
                    var objName = go != null ? go.name : "<null>";
                    var hierarchyPath = BuildHierarchyPath(go != null ? go.transform : null);
                    list.Add(new PortalEntry(portal.Id, objName, hierarchyPath));
                }
            });

            list.Sort((a, b) => string.CompareOrdinal(a.ObjectName, b.ObjectName));
            return list.ToArray();
        }

        private static string BuildHierarchyPath(UnityEngine.Transform t) {
            if (t == null) {
                return string.Empty;
            }

            var path = t.name;
            var cur = t.parent;
            while (cur != null) {
                path = cur.name + "/" + path;
                cur = cur.parent;
            }

            return path;
        }
    }
}
#endif
