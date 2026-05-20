using System.Collections.Generic;
using Game.Editor.SceneTopology.Model;
using UnityEngine;

namespace Game.Editor.SceneTopology {
    /// <summary>
    /// Pure positioning pass: given a scanned graph, returns the canvas top-left position for every
    /// scene card. Entrance and EntranceHorizontal edges drive spatial placement: linked scenes are
    /// placed as close to each other as possible, with the shared portal pins aligned. Cross-scene door
    /// edges do NOT drive placement at all — they're rendered as dashed lines wherever the endpoints
    /// end up. Any scene not pulled into a cluster falls into the unlinked grid at the bottom.
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
        /// <summary>Springs-only warmup iterations: collapse to the shortest-link arrangement, overlaps allowed.</summary>
        private const int RelaxWarmupIterations = 60;
        /// <summary>Force-relaxation iterations applied to each seeded cluster (spring + separation).</summary>
        private const int RelaxIterations = 80;
        /// <summary>Per-iteration fraction of the link gap closed by the pin-coincidence spring.</summary>
        private const float RelaxStiffness = 0.1f;
        /// <summary>Per-iteration fraction of overlap removed during the combined relaxation pass.</summary>
        private const float RelaxSeparationFactor = 0.5f;
        /// <summary>Extra perpendicular stiffness that snaps a link onto its preferred axis (Entrance →
        /// horizontal, EntranceHorizontal → vertical), added on top of <see cref="RelaxStiffness"/>.</summary>
        private const float RelaxAlignStiffness = 0.3f;
        /// <summary>Link run length (px) at which the alignment preference has fully faded, freeing an
        /// otherwise very long axis-aligned link to settle as a shorter diagonal instead.</summary>
        private const float RelaxMaxAlignedLength = 600f;
        /// <summary>Separation-only settle iterations after relaxation to clear any residual overlaps.</summary>
        private const int RelaxSettleIterations = 12;

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

            // Adjacency for placement-driving edges (Entrance + EntranceHorizontal). Doors are excluded.
            var placementAdj = BuildAdjacency(graph);

            // Components, largest first, deterministic tiebreak by GUID.
            var components = FindComponents(graph.Nodes, placementAdj);
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

                var clusterPositions = LayoutComponent(members, placementAdj, result.CardSizes, nodesByGuid);
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

        /// <summary>
        /// Portal kinds that pull linked scenes into a spatial cluster. Entrance and its horizontal
        /// variant both drive placement; doors are drawn but never move cards.
        /// </summary>
        private static bool DrivesPlacement(PortalKindRef kind) {
            return kind == PortalKindRef.Entrance || kind == PortalKindRef.EntranceHorizontal;
        }

        private static Dictionary<string, List<(string otherGuid, PortalData fromPortal, PortalData toPortal)>>
            BuildAdjacency(SceneTopologyGraph graph) {
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
                if (!DrivesPlacement(e.Kind)) {
                    continue;
                }

                if (!nodesByGuid.TryGetValue(e.FromSceneGuid, out var fromNode)) {
                    continue;
                }

                if (!nodesByGuid.TryGetValue(e.ToSceneGuid, out var toNode)) {
                    continue;
                }

                var fromPortal = FindPortal(fromNode, e.Kind, e.FromPortalId);
                var toPortal = FindPortal(toNode, e.Kind, e.ToPortalId);
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
        /// Two stages. First a spanning-tree seed (BFS): place the seed scene, then for each node visited
        /// in BFS order, position every unprocessed direct neighbor pin-coincident, slid along the parent
        /// pin's outward cardinal axis just enough to satisfy MinGap against already-placed cards. This
        /// alone paints later cards into corners — a card whose ideal slot is occupied gets shoved far,
        /// stretching its link. So a second stage (<see cref="RelaxComponent"/>) treats the seed as a
        /// starting guess and lets every card move under link springs + overlap separation, pulling
        /// stretched links back into short diagonals.
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
                    Vector2 slideDir;
                    if (fromPortal.Kind == PortalKindRef.EntranceHorizontal) {
                        // Horizontal entrances join the top/bottom edges of scenes, so linked scenes
                        // belong one above the other. Force the vertical axis rather than inferring it:
                        // the trigger usually sits mid-card, where the horizontal offset would otherwise
                        // win and push the neighbor out to the side.
                        slideDir = new Vector2(0f, rawOutward.y >= 0f ? 1f : -1f);
                    } else {
                        slideDir = Mathf.Abs(rawOutward.x) >= Mathf.Abs(rawOutward.y)
                            ? new Vector2(rawOutward.x >= 0f ? 1f : -1f, 0f)
                            : new Vector2(0f, rawOutward.y >= 0f ? 1f : -1f);
                    }

                    // Ideal pin-coincident position: the link has length zero here. Slide afterwards
                    // until the gap constraint is met.
                    var idealPos = curPinWorld - PinLocalPixels(otherSize, otherBounds, toPortal.LocalPos);
                    var otherPos = SlideUntilFree(idealPos, otherSize, slideDir, positions, cardSizes, collisionPad);

                    positions[other] = otherPos;
                    queue.Enqueue(other);
                }
            }

            // Refine the greedy seed: let cards move so detoured links contract (see method summary).
            RelaxComponent(positions, members, adj, cardSizes, nodesByGuid, collisionPad);
            return positions;
        }

        /// <summary>
        /// Refines a seeded cluster layout with a deterministic three-phase force pass so linked scenes
        /// end up close together regardless of the order the seed placed them in:
        /// <list type="number">
        /// <item><b>Springs only</b> — with separation off, cards pass freely through one another and the
        /// component collapses to the globally shortest-link arrangement (pure spring energy is convex, so
        /// the optimum is unique and independent of the greedy seed). This is what frees a pair the seed
        /// drove apart — a blocker squatting on the direct slot — to collapse back together.</item>
        /// <item><b>Springs + separation</b> — spread the overlapping pile apart while springs hold linked
        /// cards adjacent, so cards stop overlapping but links stay short. Only actual overlaps repel
        /// (no global all-pairs repulsion), so clusters stay compact.</item>
        /// <item><b>Separation only</b> — clear any residual overlap so no two cards violate MinGap.</item>
        /// </list>
        /// </summary>
        private static void RelaxComponent(Dictionary<string, Vector2> positions, List<string> members,
            Dictionary<string, List<(string otherGuid, PortalData fromPortal, PortalData toPortal)>> adj,
            Dictionary<string, Vector2> cardSizes, Dictionary<string, SceneNodeData> nodesByGuid,
            float collisionPad) {
            if (members.Count < 2) {
                return;
            }

            // Deterministic processing order so Refresh reproduces the same layout.
            var ordered = new List<string>(members);
            ordered.Sort(System.StringComparer.Ordinal);
            var disp = new Dictionary<string, Vector2>(ordered.Count);

            // Phase 1 — springs only: collapse to the shortest-link arrangement, overlaps allowed.
            for (var iter = 0; iter < RelaxWarmupIterations; iter++) {
                ResetDisplacements(disp, ordered);
                AccumulateLinkSprings(disp, ordered, positions, adj, cardSizes, nodesByGuid);
                ApplyDisplacements(disp, ordered, positions);
            }

            // Phase 2 — springs + separation: spread the pile apart while keeping links short.
            for (var iter = 0; iter < RelaxIterations; iter++) {
                ResetDisplacements(disp, ordered);
                AccumulateLinkSprings(disp, ordered, positions, adj, cardSizes, nodesByGuid);
                AccumulateSeparation(disp, ordered, positions, cardSizes, collisionPad, RelaxSeparationFactor);
                ApplyDisplacements(disp, ordered, positions);
            }

            // Phase 3 — separation-only settle: clear residual overlaps. A linked pair may end a hair
            // farther apart — the accepted "slightly longer link to avoid an overlap" trade. Stops early
            // once nothing overlaps.
            for (var iter = 0; iter < RelaxSettleIterations; iter++) {
                ResetDisplacements(disp, ordered);
                if (!AccumulateSeparation(disp, ordered, positions, cardSizes, collisionPad, 1f)) {
                    break;
                }

                ApplyDisplacements(disp, ordered, positions);
            }
        }

        private static void ResetDisplacements(Dictionary<string, Vector2> disp, List<string> ordered) {
            for (var i = 0; i < ordered.Count; i++) {
                disp[ordered[i]] = Vector2.zero;
            }
        }

        /// <summary>
        /// Each placement-link pulls its two cards toward pin alignment by a fraction of the current gap.
        /// The pull is anisotropic: the perpendicular axis — the one the pins should share to keep the link
        /// straight — gets an extra <see cref="RelaxAlignStiffness"/> boost, so Entrance links settle
        /// horizontal and EntranceHorizontal links settle vertical. That boost fades out past
        /// <see cref="RelaxMaxAlignedLength"/>, letting a link that would otherwise have to stretch very far
        /// to stay aligned take a shorter diagonal instead. Processes every undirected link once (the
        /// adjacency stores both directions) and skips same-scene self-links.
        /// </summary>
        private static void AccumulateLinkSprings(Dictionary<string, Vector2> disp, List<string> ordered,
            Dictionary<string, Vector2> positions,
            Dictionary<string, List<(string otherGuid, PortalData fromPortal, PortalData toPortal)>> adj,
            Dictionary<string, Vector2> cardSizes, Dictionary<string, SceneNodeData> nodesByGuid) {
            for (var i = 0; i < ordered.Count; i++) {
                var a = ordered[i];
                foreach (var (b, fromPortal, toPortal) in adj[a]) {
                    // Each undirected link appears as a->b and b->a; handle it once. Skips self-links (a==b).
                    if (string.CompareOrdinal(a, b) >= 0) {
                        continue;
                    }

                    var aPin = positions[a] + PinLocalPixels(cardSizes[a], nodesByGuid[a].BoundsLocal.size, fromPortal.LocalPos);
                    var bPin = positions[b] + PinLocalPixels(cardSizes[b], nodesByGuid[b].BoundsLocal.size, toPortal.LocalPos);
                    var delta = bPin - aPin;

                    // Split the gap into the link's run (parallel) and the off-axis drift (perpendicular).
                    // Entrance links run horizontally → align Y; EntranceHorizontal run vertically → align X.
                    var preferVertical = fromPortal.Kind == PortalKindRef.EntranceHorizontal;
                    var parallel = preferVertical ? new Vector2(0f, delta.y) : new Vector2(delta.x, 0f);
                    var perpendicular = delta - parallel;

                    // Boost the perpendicular pull so the link snaps straight, but fade the boost as the run
                    // grows so a link that can't stay aligned without becoming very long goes diagonal.
                    var alignFade = Mathf.Clamp01(1f - (parallel.magnitude / RelaxMaxAlignedLength));
                    var perpStiffness = RelaxStiffness + (RelaxAlignStiffness * alignFade);

                    // Apply a fraction per iteration; splitting between the two cards keeps it stable.
                    var pull = ((parallel * RelaxStiffness) + (perpendicular * perpStiffness)) * 0.5f;
                    disp[a] += pull;
                    disp[b] -= pull;
                }
            }
        }

        /// <summary>
        /// Pushes overlapping card pairs apart along their least-penetration axis, by the given fraction
        /// of the overlap, split between the two cards. Rects are inflated by <paramref name="collisionPad"/>
        /// so the resulting gap matches MinGap. Returns true if any pair overlapped this pass.
        /// </summary>
        private static bool AccumulateSeparation(Dictionary<string, Vector2> disp, List<string> ordered,
            Dictionary<string, Vector2> positions, Dictionary<string, Vector2> cardSizes,
            float collisionPad, float factor) {
            var any = false;
            for (var i = 0; i < ordered.Count; i++) {
                var aRect = InflateRect(new Rect(positions[ordered[i]], cardSizes[ordered[i]]), collisionPad);
                for (var j = i + 1; j < ordered.Count; j++) {
                    var bRect = InflateRect(new Rect(positions[ordered[j]], cardSizes[ordered[j]]), collisionPad);
                    if (!Overlaps(aRect, bRect)) {
                        continue;
                    }

                    var half = MinimumTranslation(aRect, bRect) * (factor * 0.5f);
                    disp[ordered[i]] += half;
                    disp[ordered[j]] -= half;
                    any = true;
                }
            }

            return any;
        }

        /// <summary>
        /// Smallest vector that moves rect <paramref name="a"/> out of rect <paramref name="b"/>, along
        /// whichever axis they overlap least. Assumes the rects already overlap; ties resolve to the X axis.
        /// </summary>
        private static Vector2 MinimumTranslation(Rect a, Rect b) {
            var overlapX = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
            var overlapY = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            if (overlapX <= overlapY) {
                return new Vector2(overlapX * (a.center.x >= b.center.x ? 1f : -1f), 0f);
            }

            return new Vector2(0f, overlapY * (a.center.y >= b.center.y ? 1f : -1f));
        }

        private static void ApplyDisplacements(Dictionary<string, Vector2> disp, List<string> ordered,
            Dictionary<string, Vector2> positions) {
            for (var i = 0; i < ordered.Count; i++) {
                positions[ordered[i]] += disp[ordered[i]];
            }
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
