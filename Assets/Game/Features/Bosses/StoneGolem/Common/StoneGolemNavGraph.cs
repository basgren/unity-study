using System.Collections.Generic;
using UnityEngine;

namespace Game.Features.Bosses.StoneGolem {
    /// <summary>
    /// Arena navigation graph built from the golem's <see cref="StoneGolemAnchorPoint"/> markers and
    /// their authored connections. Edges are undirected (auto-mirrored) and weighted by world
    /// distance; <see cref="FindPath"/> returns the shortest typed route between two anchors via
    /// Dijkstra. Pure data + query — no Unity lifecycle. The graph is small (a handful of nodes), so
    /// the linear-scan Dijkstra below is more than fast enough.
    /// </summary>
    public class StoneGolemNavGraph {
        /// <summary>One leg of a route: the anchor to move to and the movement type to get there.</summary>
        public struct NavStep {
            public StoneGolemAnchorPoint node;
            public StoneGolemMovementType type;

            public NavStep(StoneGolemAnchorPoint node, StoneGolemMovementType type) {
                this.node = node;
                this.type = type;
            }
        }

        private struct Edge {
            public StoneGolemAnchorPoint to;
            public StoneGolemMovementType type;
            public float cost;
        }

        private readonly Dictionary<StoneGolemAnchorPoint, List<Edge>> adjacency = new();

        public StoneGolemNavGraph(StoneGolemAnchorPoint[] anchors) {
            Build(anchors);
        }

        private void Build(StoneGolemAnchorPoint[] anchors) {
            if (anchors == null) {
                return;
            }

            for (int i = 0; i < anchors.Length; i++) {
                StoneGolemAnchorPoint a = anchors[i];
                if (a == null) {
                    continue;
                }

                EnsureNode(a);
                IReadOnlyList<StoneGolemAnchorPoint.AnchorConnection> conns = a.Connections;
                if (conns == null) {
                    continue;
                }

                for (int c = 0; c < conns.Count; c++) {
                    StoneGolemAnchorPoint b = conns[c].target;
                    if (b == null || b == a) {
                        continue;
                    }

                    // Auto-mirror: an authored A→B link is also walkable B→A with the same type.
                    AddEdge(a, b, conns[c].type);
                    AddEdge(b, a, conns[c].type);
                }
            }
        }

        private void EnsureNode(StoneGolemAnchorPoint node) {
            if (!adjacency.ContainsKey(node)) {
                adjacency[node] = new List<Edge>();
            }
        }

        private void AddEdge(StoneGolemAnchorPoint from, StoneGolemAnchorPoint to, StoneGolemMovementType type) {
            EnsureNode(from);
            EnsureNode(to);

            List<Edge> edges = adjacency[from];
            for (int i = 0; i < edges.Count; i++) {
                if (edges[i].to == to) {
                    return; // De-dupe: keep the first authored edge for this pair.
                }
            }

            edges.Add(new Edge {
                to = to,
                type = type,
                cost = Vector2.Distance(from.Position, to.Position)
            });
        }

        /// <summary>
        /// Shortest typed route from <paramref name="from"/> to <paramref name="to"/> (Dijkstra,
        /// weighted by world distance). Returned steps exclude the start node. Returns null when either
        /// node is unknown to the graph or no route exists; an empty list when from == to.
        /// </summary>
        public IReadOnlyList<NavStep> FindPath(StoneGolemAnchorPoint from, StoneGolemAnchorPoint to) {
            if (from == null || to == null || !adjacency.ContainsKey(from) || !adjacency.ContainsKey(to)) {
                return null;
            }

            if (from == to) {
                return new List<NavStep>();
            }

            var dist = new Dictionary<StoneGolemAnchorPoint, float>();
            var prev = new Dictionary<StoneGolemAnchorPoint, StoneGolemAnchorPoint>();
            var prevType = new Dictionary<StoneGolemAnchorPoint, StoneGolemMovementType>();
            var unvisited = new List<StoneGolemAnchorPoint>();

            foreach (StoneGolemAnchorPoint node in adjacency.Keys) {
                dist[node] = float.PositiveInfinity;
                unvisited.Add(node);
            }

            dist[from] = 0f;

            while (unvisited.Count > 0) {
                // Pick the unvisited node with the smallest tentative distance (small graph → linear scan).
                int bestIdx = -1;
                float best = float.PositiveInfinity;
                for (int i = 0; i < unvisited.Count; i++) {
                    if (dist[unvisited[i]] < best) {
                        best = dist[unvisited[i]];
                        bestIdx = i;
                    }
                }

                if (bestIdx < 0) {
                    break; // Remaining nodes are unreachable from the start.
                }

                StoneGolemAnchorPoint current = unvisited[bestIdx];
                unvisited.RemoveAt(bestIdx);

                if (current == to) {
                    break;
                }

                List<Edge> edges = adjacency[current];
                for (int e = 0; e < edges.Count; e++) {
                    float alt = dist[current] + edges[e].cost;
                    if (alt < dist[edges[e].to]) {
                        dist[edges[e].to] = alt;
                        prev[edges[e].to] = current;
                        prevType[edges[e].to] = edges[e].type;
                    }
                }
            }

            if (!prev.ContainsKey(to)) {
                return null; // No route.
            }

            // Reconstruct the route from the goal back to the start, then flip it.
            var reversed = new List<NavStep>();
            StoneGolemAnchorPoint step = to;
            while (step != from) {
                reversed.Add(new NavStep(step, prevType[step]));
                step = prev[step];
            }

            reversed.Reverse();
            return reversed;
        }
    }
}
