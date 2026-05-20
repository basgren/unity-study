using System.Collections.Generic;
using System.IO;
using Game.Editor.SceneTopology.Model;
using Game.Features.Portals.Common;
using Game.Features.Portals.Doors;
using Game.Features.Portals.Entrance;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor.SceneTopology {
    /// <summary>
    /// Opens each Build-Settings scene in turn, reads its CameraConfiner bounds and IPortal components,
    /// then builds a SceneTopologyGraph. Already-loaded scenes are reused in place (not reopened) so the
    /// user's setup is preserved. The scan aborts cleanly if any open scene is dirty — we never auto-save.
    /// </summary>
    public static class SceneTopologyScanner {
        private const string CameraConfinerName = "CameraConfiner";
        private const string ScenesRoot = "Assets/Game/Scenes/";
        private const string RootRegionKey = "_root";
        private static readonly Vector2 FallbackBoundsSize = new Vector2(20f, 12f);

        /// <summary>
        /// Scans every enabled scene in Build Settings. Returns null when the user cancelled or when
        /// the open scene state was dirty (in which case an explanatory dialog has already been shown).
        /// Reuses per-scene snapshots from <see cref="SceneTopologyCache"/> whenever the scene asset's
        /// <c>AssetDatabase</c> dependency hash matches the cached value — only changed scenes are opened.
        /// </summary>
        public static SceneTopologyGraph Scan() {
            var scenePaths = CollectBuildScenePaths();
            if (scenePaths.Count == 0) {
                EditorUtility.DisplayDialog(
                    "Scene Topology",
                    "No enabled scenes found in Build Settings.",
                    "OK");
                return null;
            }

            // Compute fresh hashes up front so we know how many scenes actually need loading.
            // The hash key includes the scene asset and its dependencies (sprites, prefabs, etc.).
            var cache = SceneTopologyCache.Load();
            var sceneGuids = new string[scenePaths.Count];
            var freshHashes = new string[scenePaths.Count];
            var needsLoad = new bool[scenePaths.Count];
            var loadCount = 0;
            for (var i = 0; i < scenePaths.Count; i++) {
                var path = scenePaths[i];
                sceneGuids[i] = AssetDatabase.AssetPathToGUID(path);
                freshHashes[i] = AssetDatabase.GetAssetDependencyHash(path).ToString();
                if (!cache.TryGetValue(sceneGuids[i], out var cached) || cached.DependencyHash != freshHashes[i]) {
                    needsLoad[i] = true;
                    loadCount++;
                }
            }

            // Only block on dirty open scenes when we're actually about to open one additively.
            // Cache-only refreshes don't touch the scene manager and are safe.
            if (loadCount > 0 && HasDirtyOpenScene()) {
                EditorUtility.DisplayDialog(
                    "Scene Topology",
                    "Save or discard changes in your open scenes before refreshing.\n\n" +
                    "Some scenes need to be (re)scanned, which requires opening them additively.",
                    "OK");
                return null;
            }

            var alreadyOpen = CollectOpenSceneMap();
            var graph = new SceneTopologyGraph();
            var openedForScan = new List<Scene>();
            var nextCache = new Dictionary<string, SceneTopologyCache.Entry>(scenePaths.Count);
            var loadIndex = 0;

            try {
                for (var i = 0; i < scenePaths.Count; i++) {
                    var path = scenePaths[i];
                    var guid = sceneGuids[i];
                    var hash = freshHashes[i];

                    if (!needsLoad[i] && cache.TryGetValue(guid, out var cached)) {
                        // Path-derived fields stay fresh even if the asset was moved with the same GUID.
                        cached.Node.ScenePath = path;
                        cached.Node.DisplayName = Path.GetFileNameWithoutExtension(path);
                        cached.Node.RegionKey = ResolveRegionKey(path);
                        graph.Nodes.Add(cached.Node);
                        graph.CachedNodeCount++;
                        nextCache[guid] = cached;
                        continue;
                    }

                    // Progress only over scenes that actually need loading — that's the slow part.
                    var progress = loadCount > 0 ? (float)loadIndex / loadCount : 0f;
                    if (EditorUtility.DisplayCancelableProgressBar("Scene Topology", "Scanning " + path, progress)) {
                        return null;
                    }

                    loadIndex++;

                    Scene scene;
                    bool weOpenedIt;
                    if (alreadyOpen.TryGetValue(path, out var existing)) {
                        scene = existing;
                        weOpenedIt = false;
                    } else {
                        scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                        weOpenedIt = true;
                        openedForScan.Add(scene);
                    }

                    var node = BuildNode(scene, path);
                    graph.Nodes.Add(node);
                    graph.FreshlyScannedNodeCount++;
                    nextCache[guid] = new SceneTopologyCache.Entry {
                        DependencyHash = hash,
                        Node = node,
                    };

                    // Close immediately if we opened it; keeps memory pressure low across many scenes.
                    if (weOpenedIt) {
                        EditorSceneManager.CloseScene(scene, removeScene: true);
                        openedForScan.RemoveAt(openedForScan.Count - 1);
                    }
                }
            } finally {
                EditorUtility.ClearProgressBar();
                // Defensive cleanup: anything still in openedForScan means an exception bailed mid-scan.
                for (var i = 0; i < openedForScan.Count; i++) {
                    if (openedForScan[i].IsValid()) {
                        EditorSceneManager.CloseScene(openedForScan[i], removeScene: true);
                    }
                }
            }

            BuildEdges(graph);
            SceneTopologyCache.Save(nextCache);
            return graph;
        }

        private static bool HasDirtyOpenScene() {
            for (var i = 0; i < EditorSceneManager.sceneCount; i++) {
                var s = EditorSceneManager.GetSceneAt(i);
                if (s.isDirty) {
                    return true;
                }
            }

            return false;
        }

        private static Dictionary<string, Scene> CollectOpenSceneMap() {
            var map = new Dictionary<string, Scene>();
            for (var i = 0; i < EditorSceneManager.sceneCount; i++) {
                var s = EditorSceneManager.GetSceneAt(i);
                if (s.IsValid() && !string.IsNullOrEmpty(s.path)) {
                    map[s.path] = s;
                }
            }

            return map;
        }

        private static List<string> CollectBuildScenePaths() {
            var paths = new List<string>();
            var scenes = EditorBuildSettings.scenes;
            for (var i = 0; i < scenes.Length; i++) {
                if (scenes[i].enabled && !string.IsNullOrEmpty(scenes[i].path)) {
                    paths.Add(scenes[i].path);
                }
            }

            return paths;
        }

        private static SceneNodeData BuildNode(Scene scene, string path) {
            var node = new SceneNodeData {
                ScenePath = path,
                SceneGuid = AssetDatabase.AssetPathToGUID(path),
                DisplayName = Path.GetFileNameWithoutExtension(path),
                RegionKey = ResolveRegionKey(path),
            };

            var (boundsRect, hasConfiner) = ReadConfinerBounds(scene);
            node.BoundsLocal = new Rect(Vector2.zero, boundsRect.size);
            node.BoundsAreFallback = !hasConfiner;

            // Use bounds.min in world space as the origin for portal local positions.
            var origin = boundsRect.min;
            CollectPortals<Entrance>(scene, PortalKindRef.Entrance, origin, boundsRect.size, hasConfiner, node);
            CollectPortals<EntranceHorizontal>(scene, PortalKindRef.EntranceHorizontal, origin, boundsRect.size, hasConfiner, node);
            CollectPortals<Door>(scene, PortalKindRef.Door, origin, boundsRect.size, hasConfiner, node);
            return node;
        }

        private static (Rect rect, bool hasConfiner) ReadConfinerBounds(Scene scene) {
            PolygonCollider2D confiner = null;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length && confiner == null; i++) {
                // Search by name through children — CameraConfiner is conventionally a named GO in each scene.
                var t = FindChildByName(roots[i].transform, CameraConfinerName);
                if (t != null) {
                    confiner = t.GetComponent<PolygonCollider2D>();
                }
            }

            if (confiner == null) {
                return (new Rect(Vector2.zero, FallbackBoundsSize), false);
            }

            // PolygonCollider2D.bounds is world-space AABB already accounting for transform.
            var b = confiner.bounds;
            return (new Rect(b.min, b.size), true);
        }

        private static Transform FindChildByName(Transform root, string name) {
            if (root.name == name) {
                return root;
            }

            for (var i = 0; i < root.childCount; i++) {
                var hit = FindChildByName(root.GetChild(i), name);
                if (hit != null) {
                    return hit;
                }
            }

            return null;
        }

        private static void CollectPortals<T>(Scene scene, PortalKindRef kind, Vector2 origin, Vector2 size,
            bool hasConfiner, SceneNodeData node) where T : Component, IPortal {
            var portals = PortalUtils.GetPortalsInScene<T>(scene);
            for (var i = 0; i < portals.Count; i++) {
                var p = (IPortal)portals[i];
                var world = (Vector2)p.GetEntryPosition();
                var localWorldY = world - origin;
                // Convert to canvas convention: Y-down with origin at the card's top-left.
                var local = new Vector2(localWorldY.x, size.y - localWorldY.y);
                if (!hasConfiner) {
                    // No confiner: clamp into the fallback rect so pins stay visible on the card.
                    local = new Vector2(
                        Mathf.Clamp(local.x, 0f, size.x),
                        Mathf.Clamp(local.y, 0f, size.y));
                }

                node.Portals.Add(new PortalData {
                    Id = p.Id,
                    Kind = kind,
                    LocalPos = local,
                    TargetSceneGuid = p.TargetScene.SceneGuid,
                    TargetId = p.TargetId,
                });
            }
        }

        private static string ResolveRegionKey(string path) {
            if (!path.StartsWith(ScenesRoot, System.StringComparison.OrdinalIgnoreCase)) {
                return RootRegionKey;
            }

            var rel = path.Substring(ScenesRoot.Length);
            var slash = rel.IndexOf('/');
            if (slash < 0) {
                return RootRegionKey;
            }

            return rel.Substring(0, slash);
        }

        private static void BuildEdges(SceneTopologyGraph graph) {
            var nodesByGuid = new Dictionary<string, SceneNodeData>(graph.Nodes.Count);
            for (var i = 0; i < graph.Nodes.Count; i++) {
                nodesByGuid[graph.Nodes[i].SceneGuid] = graph.Nodes[i];
            }

            // Dedupe undirected pairs so we don't render the same A->B and B->A twice.
            var seen = new HashSet<string>();
            for (var i = 0; i < graph.Nodes.Count; i++) {
                var from = graph.Nodes[i];
                for (var p = 0; p < from.Portals.Count; p++) {
                    var portal = from.Portals[p];
                    if (string.IsNullOrEmpty(portal.TargetSceneGuid) || string.IsNullOrEmpty(portal.TargetId)) {
                        continue;
                    }

                    if (!nodesByGuid.TryGetValue(portal.TargetSceneGuid, out var to)) {
                        graph.Warnings.Add($"{from.DisplayName} #{portal.Id} ({portal.Kind}) → unknown scene GUID {portal.TargetSceneGuid}");
                        continue;
                    }

                    var targetPortal = FindPortal(to, portal.Kind, portal.TargetId);
                    if (targetPortal == null) {
                        graph.Warnings.Add($"{from.DisplayName} #{portal.Id} ({portal.Kind}) → {to.DisplayName}#{portal.TargetId} not found");
                        continue;
                    }

                    var key = MakeUndirectedKey(from.SceneGuid, portal.Id, to.SceneGuid, portal.TargetId, portal.Kind);
                    if (!seen.Add(key)) {
                        continue;
                    }

                    graph.Edges.Add(new PortalEdgeData {
                        FromSceneGuid = from.SceneGuid,
                        FromPortalId = portal.Id,
                        ToSceneGuid = to.SceneGuid,
                        ToPortalId = portal.TargetId,
                        Kind = portal.Kind,
                    });
                }
            }
        }

        private static PortalData FindPortal(SceneNodeData node, PortalKindRef kind, string id) {
            for (var i = 0; i < node.Portals.Count; i++) {
                if (node.Portals[i].Kind == kind && node.Portals[i].Id == id) {
                    return node.Portals[i];
                }
            }

            return null;
        }

        private static string MakeUndirectedKey(string aGuid, string aId, string bGuid, string bId, PortalKindRef kind) {
            // Lexicographic order makes the pair direction-agnostic.
            var aKey = aGuid + "#" + aId;
            var bKey = bGuid + "#" + bId;
            if (string.CompareOrdinal(aKey, bKey) <= 0) {
                return kind + "|" + aKey + "|" + bKey;
            }

            return kind + "|" + bKey + "|" + aKey;
        }
    }
}
