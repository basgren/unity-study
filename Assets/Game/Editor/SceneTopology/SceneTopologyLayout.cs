using System.Collections.Generic;
using Game.Editor.SceneTopology.Model;
using UnityEngine;

namespace Game.Editor.SceneTopology {
    /// <summary>
    /// Pure positioning pass: given a scanned graph, returns the canvas top-left position for every
    /// scene card. Entrance-to-entrance edges drive spatial placement: linked scenes are placed as
    /// close to each other as possible, with the shared entrance pins aligned. Cross-scene door edges
    /// do NOT drive placement at all — they're rendered as dashed lines wherever the endpoints end up.
    /// Any scene not pulled into an entrance cluster falls into the unlinked grid at the bottom.
    ///
    /// Coordinates are returned in canvas pixels. Y goes down (standard UI Toolkit convention).
    /// </summary>
    public static class SceneTopologyLayout {
        /// <summary>How many canvas pixels per scene world unit. Bigger = larger cards and farther apart.</summary>
        public const float PixelsPerUnit = 8f;
        /// <summary>Minimum card size in pixels — tiny scenes still need to be readable.</summary>
        public static readonly Vector2 MinCardSize = new Vector2(140f, 90f);
        /// <summary>Maximum card size in pixels — keeps huge scenes from dominating the view.</summary>
        public static readonly Vector2 MaxCardSize = new Vector2(520f, 360f);
        /// <summary>
        /// Minimum gap to keep between scene cards, in scene world units (1 unit = 1 Unity unit).
        /// Cards always sit at least this far apart. Adjust to taste; window has no UI for it yet.
        /// </summary>
        public const float MinGapWorldUnits = 1f;
        /// <summary>Cap on placement-slide iterations so a degenerate scene wall can't lock the editor.</summary>
        private const int PlacementMaxSlideIterations = 200;
        /// <summary>Gap between consecutive component clusters in the horizontal lane.</summary>
        private const float ClusterGap = 120f;
        /// <summary>Gap between the cluster lane and the unlinked grid strip.</summary>
        private const float UnlinkedStripGap = 200f;
        private const int UnlinkedGridColumns = 4;
        private const float UnlinkedGridGap = 40f;

        public sealed class Result {
            public Dictionary<string, Vector2> Positions = new Dictionary<string, Vector2>();
            public Dictionary<string, Vector2> CardSizes = new Dictionary<string, Vector2>();
        }

        public static Result Compute(SceneTopologyGraph graph) {
            var result = new Result();
            if (graph == null || graph.Nodes.Count == 0) {
                return result;
            }

            // Card sizes upfront — needed by every later step (collision, alignment, grid).
            for (var i = 0; i < graph.Nodes.Count; i++) {
                var n = graph.Nodes[i];
                result.CardSizes[n.SceneGuid] = ComputeCardSize(n);
            }

            // Index nodes by guid so the layout pass can reach back to per-scene bounds when computing
            // pin pixel offsets (cards may be clamped, so we can't reuse raw LocalPos * PixelsPerUnit).
            var nodesByGuid = new Dictionary<string, SceneNodeData>(graph.Nodes.Count);
            for (var i = 0; i < graph.Nodes.Count; i++) {
                nodesByGuid[graph.Nodes[i].SceneGuid] = graph.Nodes[i];
            }

            // Adjacency for ENTRANCE edges only — that's what drives topology.
            var entranceAdj = BuildAdjacency(graph, PortalKindRef.Entrance);

            // Components, largest first, deterministic tiebreak by GUID.
            var components = FindComponents(graph.Nodes, entranceAdj);
            components.Sort((a, b) => {
                if (a.Count != b.Count) {
                    return b.Count.CompareTo(a.Count);
                }

                return string.CompareOrdinal(a[0], b[0]);
            });

            // Place each entrance-linked component as its own cluster, lay clusters out in a horizontal lane.
            var clusterCursorX = 0f;
            var maxClusterBottom = 0f;
            var placed = new Dictionary<string, Rect>();
            for (var i = 0; i < components.Count; i++) {
                var members = components[i];
                if (members.Count == 1) {
                    // Isolated nodes are handled later in the unlinked strip.
                    continue;
                }

                var clusterPositions = LayoutComponent(members, entranceAdj, result.CardSizes, nodesByGuid);
                var bounds = OffsetCluster(clusterPositions, result.CardSizes, new Vector2(clusterCursorX, 0f));
                AppendToPlaced(clusterPositions, result.CardSizes, placed, result.Positions);
                clusterCursorX = bounds.xMax + ClusterGap;
                if (bounds.yMax > maxClusterBottom) {
                    maxClusterBottom = bounds.yMax;
                }
            }

            // Cross-scene doors do NOT influence layout — their dashed edges are drawn between
            // whatever positions the endpoints ended up at. Anything not placed by the entrance pass
            // falls through to the unlinked grid below.

            // Unlinked strip: any scene still without a position goes into a simple grid.
            var unlinked = new List<string>();
            for (var i = 0; i < graph.Nodes.Count; i++) {
                if (!placed.ContainsKey(graph.Nodes[i].SceneGuid)) {
                    unlinked.Add(graph.Nodes[i].SceneGuid);
                }
            }

            if (unlinked.Count > 0) {
                LayoutUnlinkedGrid(unlinked, result.CardSizes, placed, result.Positions, maxClusterBottom + UnlinkedStripGap);
            }

            return result;
        }

        private static Vector2 ComputeCardSize(SceneNodeData node) {
            var raw = node.BoundsLocal.size * PixelsPerUnit;
            return new Vector2(
                Mathf.Clamp(raw.x, MinCardSize.x, MaxCardSize.x),
                Mathf.Clamp(raw.y, MinCardSize.y, MaxCardSize.y));
        }

        private static Dictionary<string, List<(string otherGuid, PortalData fromPortal, PortalData toPortal)>>
            BuildAdjacency(SceneTopologyGraph graph, PortalKindRef kind) {
            var nodesByGuid = new Dictionary<string, SceneNodeData>(graph.Nodes.Count);
            for (var i = 0; i < graph.Nodes.Count; i++) {
                nodesByGuid[graph.Nodes[i].SceneGuid] = graph.Nodes[i];
            }

            var adj = new Dictionary<string, List<(string, PortalData, PortalData)>>();
            for (var i = 0; i < graph.Nodes.Count; i++) {
                adj[graph.Nodes[i].SceneGuid] = new List<(string, PortalData, PortalData)>();
            }

            for (var i = 0; i < graph.Edges.Count; i++) {
                var e = graph.Edges[i];
                if (e.Kind != kind) {
                    continue;
                }

                if (!nodesByGuid.TryGetValue(e.FromSceneGuid, out var fromNode)) {
                    continue;
                }

                if (!nodesByGuid.TryGetValue(e.ToSceneGuid, out var toNode)) {
                    continue;
                }

                var fromPortal = FindPortal(fromNode, kind, e.FromPortalId);
                var toPortal = FindPortal(toNode, kind, e.ToPortalId);
                if (fromPortal == null || toPortal == null) {
                    continue;
                }

                adj[e.FromSceneGuid].Add((e.ToSceneGuid, fromPortal, toPortal));
                adj[e.ToSceneGuid].Add((e.FromSceneGuid, toPortal, fromPortal));
            }

            return adj;
        }

        private static PortalData FindPortal(SceneNodeData node, PortalKindRef kind, string id) {
            for (var i = 0; i < node.Portals.Count; i++) {
                if (node.Portals[i].Kind == kind && node.Portals[i].Id == id) {
                    return node.Portals[i];
                }
            }

            return null;
        }

        private static List<List<string>> FindComponents(List<SceneNodeData> nodes,
            Dictionary<string, List<(string otherGuid, PortalData fromPortal, PortalData toPortal)>> adj) {
            var visited = new HashSet<string>();
            var components = new List<List<string>>();
            for (var i = 0; i < nodes.Count; i++) {
                var seed = nodes[i].SceneGuid;
                if (!visited.Add(seed)) {
                    continue;
                }

                var component = new List<string> { seed };
                var queue = new Queue<string>();
                queue.Enqueue(seed);
                while (queue.Count > 0) {
                    var cur = queue.Dequeue();
                    foreach (var (other, _, _) in adj[cur]) {
                        if (visited.Add(other)) {
                            component.Add(other);
                            queue.Enqueue(other);
                        }
                    }
                }

                components.Add(component);
            }

            return components;
        }

        /// <summary>
        /// Spanning-tree placement (BFS): place the seed, then for each node visited in BFS order,
        /// position every unprocessed direct neighbor so the link between them is the shortest possible —
        /// pin-coincident at the optimum, slid along the parent pin's outward cardinal axis just enough
        /// to satisfy the MinGap collision constraint with already-placed cards. Once a node has been
        /// placed by its BFS parent, it never moves; cross-edges (non-tree links) are drawn at whatever
        /// length the resulting positions imply.
        /// </summary>
        private static Dictionary<string, Vector2> LayoutComponent(List<string> members,
            Dictionary<string, List<(string otherGuid, PortalData fromPortal, PortalData toPortal)>> adj,
            Dictionary<string, Vector2> cardSizes,
            Dictionary<string, SceneNodeData> nodesByGuid) {
            // Seed = largest-area scene, deterministic tiebreak by GUID for repeatable layouts.
            var seed = members[0];
            var seedArea = cardSizes[seed].x * cardSizes[seed].y;
            for (var i = 1; i < members.Count; i++) {
                var area = cardSizes[members[i]].x * cardSizes[members[i]].y;
                if (area > seedArea || (Mathf.Approximately(area, seedArea) && string.CompareOrdinal(members[i], seed) < 0)) {
                    seed = members[i];
                    seedArea = area;
                }
            }

            var positions = new Dictionary<string, Vector2> { { seed, Vector2.zero } };
            var queue = new Queue<string>();
            queue.Enqueue(seed);
            var collisionPad = MinGapWorldUnits * PixelsPerUnit * 0.5f;

            while (queue.Count > 0) {
                var cur = queue.Dequeue();
                var curPos = positions[cur];
                var curSize = cardSizes[cur];
                var curBounds = nodesByGuid[cur].BoundsLocal.size;
                var curCenter = curPos + (curSize * 0.5f);

                // Place each unprocessed neighbor at its optimal position relative to `cur`, then lock it.
                // Iterate edges in a deterministic order so the resulting layout is repeatable.
                foreach (var (other, fromPortal, toPortal) in adj[cur]) {
                    if (positions.ContainsKey(other)) {
                        continue;
                    }

                    var otherSize = cardSizes[other];
                    var otherBounds = nodesByGuid[other].BoundsLocal.size;
                    // Use the same normalized pin-in-card formula as the canvas renderer. Otherwise,
                    // when a card is clamped to Min/MaxCardSize, the layout would align pins in raw
                    // pixel space while the renderer places them elsewhere, producing diagonal links.
                    var curPinWorld = curPos + PinLocalPixels(curSize, curBounds, fromPortal.LocalPos);

                    // Slide direction = cardinal axis pointing away from the parent card center through
                    // the pin. For a pin on the right edge of `cur` this resolves to (1,0); top edge to
                    // (0,-1); etc. We slide along this axis to satisfy MinGap.
                    var rawOutward = curPinWorld - curCenter;
                    var slideDir = Mathf.Abs(rawOutward.x) >= Mathf.Abs(rawOutward.y)
                        ? new Vector2(rawOutward.x >= 0f ? 1f : -1f, 0f)
                        : new Vector2(0f, rawOutward.y >= 0f ? 1f : -1f);

                    // Ideal pin-coincident position: the link has length zero here. Slide afterwards
                    // until the gap constraint is met.
                    var idealPos = curPinWorld - PinLocalPixels(otherSize, otherBounds, toPortal.LocalPos);
                    var otherPos = SlideUntilFree(idealPos, otherSize, slideDir, positions, cardSizes, collisionPad);

                    positions[other] = otherPos;
                    queue.Enqueue(other);
                }
            }

            return positions;
        }

        /// <summary>
        /// Pin offset inside its card, in canvas pixels. Mirrors how the canvas renderer positions
        /// pins: normalized within bounds, then scaled to the (possibly clamped) card size. Layout
        /// must use this formula so it aligns the same pixel positions the renderer will draw.
        /// </summary>
        public static Vector2 PinLocalPixels(Vector2 cardSize, Vector2 boundsSize, Vector2 portalLocal) {
            if (boundsSize.x <= 0f || boundsSize.y <= 0f) {
                return cardSize * 0.5f;
            }

            var normX = Mathf.Clamp01(portalLocal.x / boundsSize.x);
            var normY = Mathf.Clamp01(portalLocal.y / boundsSize.y);
            return new Vector2(normX * cardSize.x, normY * cardSize.y);
        }

        /// <summary>
        /// Starting from <paramref name="candidate"/>, slides a rectangle of <paramref name="size"/> along
        /// <paramref name="slideDir"/> (a unit cardinal vector) until it no longer overlaps any of the
        /// rectangles in <paramref name="placed"/>, with each rect padded by <paramref name="collisionPad"/>
        /// on every side. Each iteration computes the maximum push required to clear the closest blocker
        /// in the slide direction, applies it (plus a tiny epsilon), and retries.
        /// </summary>
        private static Vector2 SlideUntilFree(Vector2 candidate, Vector2 size, Vector2 slideDir,
            Dictionary<string, Vector2> placed, Dictionary<string, Vector2> sizes, float collisionPad) {
            const float epsilon = 0.01f;
            var pos = candidate;
            var horizontal = Mathf.Abs(slideDir.x) > 0.5f;

            for (var iter = 0; iter < PlacementMaxSlideIterations; iter++) {
                var maxPush = 0f;
                var otherRect = InflateRect(new Rect(pos, size), collisionPad);
                foreach (var kvp in placed) {
                    var placedRect = InflateRect(new Rect(kvp.Value, sizes[kvp.Key]), collisionPad);
                    if (!Overlaps(otherRect, placedRect)) {
                        continue;
                    }

                    // Distance to push along slideDir to put otherRect's trailing edge past placedRect.
                    float push;
                    if (horizontal) {
                        push = slideDir.x > 0f
                            ? placedRect.xMax - otherRect.xMin
                            : otherRect.xMax - placedRect.xMin;
                    } else {
                        push = slideDir.y > 0f
                            ? placedRect.yMax - otherRect.yMin
                            : otherRect.yMax - placedRect.yMin;
                    }

                    if (push > maxPush) {
                        maxPush = push;
                    }
                }

                if (maxPush <= 0f) {
                    return pos;
                }

                pos += slideDir * (maxPush + epsilon);
            }

            return pos;
        }

        private static Rect InflateRect(Rect r, float pad) {
            return new Rect(r.xMin - pad, r.yMin - pad, r.width + (pad * 2f), r.height + (pad * 2f));
        }

        private static bool Overlaps(Rect a, Rect b) {
            return a.xMin < b.xMax && a.xMax > b.xMin && a.yMin < b.yMax && a.yMax > b.yMin;
        }

        private static Rect OffsetCluster(Dictionary<string, Vector2> clusterPositions,
            Dictionary<string, Vector2> cardSizes, Vector2 offset) {
            var minX = float.PositiveInfinity;
            var minY = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var maxY = float.NegativeInfinity;
            foreach (var kvp in clusterPositions) {
                if (kvp.Value.x < minX) {
                    minX = kvp.Value.x;
                }

                if (kvp.Value.y < minY) {
                    minY = kvp.Value.y;
                }
            }

            // Re-anchor so the cluster's top-left starts at the offset point.
            var anchor = new Vector2(minX, minY);
            var keys = new List<string>(clusterPositions.Keys);
            for (var i = 0; i < keys.Count; i++) {
                clusterPositions[keys[i]] = clusterPositions[keys[i]] - anchor + offset;
                var pos = clusterPositions[keys[i]];
                var size = cardSizes[keys[i]];
                if (pos.x < minX) {
                    minX = pos.x;
                }

                if (pos.y < minY) {
                    minY = pos.y;
                }

                if (pos.x + size.x > maxX) {
                    maxX = pos.x + size.x;
                }

                if (pos.y + size.y > maxY) {
                    maxY = pos.y + size.y;
                }
            }

            return Rect.MinMaxRect(offset.x, offset.y,
                float.IsNegativeInfinity(maxX) ? offset.x : maxX,
                float.IsNegativeInfinity(maxY) ? offset.y : maxY);
        }

        private static void AppendToPlaced(Dictionary<string, Vector2> source, Dictionary<string, Vector2> sizes,
            Dictionary<string, Rect> placed, Dictionary<string, Vector2> outPositions) {
            foreach (var kvp in source) {
                placed[kvp.Key] = new Rect(kvp.Value, sizes[kvp.Key]);
                outPositions[kvp.Key] = kvp.Value;
            }
        }

        private static void LayoutUnlinkedGrid(List<string> unlinked, Dictionary<string, Vector2> sizes,
            Dictionary<string, Rect> placed, Dictionary<string, Vector2> outPositions, float topY) {
            unlinked.Sort(System.StringComparer.Ordinal); // deterministic order
            float rowHeight = 0f;
            float x = 0f;
            float y = topY;
            int col = 0;
            for (var i = 0; i < unlinked.Count; i++) {
                var size = sizes[unlinked[i]];
                if (col >= UnlinkedGridColumns) {
                    col = 0;
                    x = 0f;
                    y += rowHeight + UnlinkedGridGap;
                    rowHeight = 0f;
                }

                outPositions[unlinked[i]] = new Vector2(x, y);
                placed[unlinked[i]] = new Rect(x, y, size.x, size.y);

                x += size.x + UnlinkedGridGap;
                if (size.y > rowHeight) {
                    rowHeight = size.y;
                }

                col++;
            }
        }
    }
}
