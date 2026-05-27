using System;
using System.Collections.Generic;
using Game.Core.Utils;
using UnityEngine;

namespace Game.Features.Bosses.StoneGolem {
    /// <summary>Which level of the phase-1 arena an anchor sits on.</summary>
    public enum StoneGolemLevel {
        Level1,
        Level2
    }

    /// <summary>How the golem travels along a graph edge between two anchors.</summary>
    public enum StoneGolemMovementType {
        /// <summary>Horizontal ground walk at constant speed (no arc). Same-level neighbours.</summary>
        Move,

        /// <summary>Parabolic jump between levels (low↔high), eased for a heavy "levitation" feel.</summary>
        Jump,

        /// <summary>Low horizontal parabolic hop across the level-2 platform gap.</summary>
        HorzJump
    }

    /// <summary>
    /// A scene-placed marker for a Stone Golem movement point, and one node of the arena navigation
    /// graph. The golem only travels along authored <see cref="Connections"/> edges: to reach a
    /// non-adjacent anchor it follows a path built by <see cref="StoneGolemNavGraph"/>. Level 1 holds
    /// the ground / ground-hit + takeoff spots, level 2 the platform landing spots.
    ///
    /// Position is the Transform; the AI holds a serialized array of these and filters by
    /// <see cref="Level"/> (drag-and-drop, no global lookup — matching the VengefulSpirit anchor
    /// convention). Edges are authored per-anchor and auto-mirrored by the graph, so each undirected
    /// link only needs wiring once.
    /// </summary>
    public class StoneGolemAnchorPoint : MonoBehaviour {
        /// <summary>One outgoing graph edge: the neighbour anchor and how the golem moves to it.</summary>
        [Serializable]
        public struct AnchorConnection {
            public StoneGolemAnchorPoint target;
            public StoneGolemMovementType type;
        }

        [SerializeField]
        [Tooltip("Which arena level this point sits on.")]
        private StoneGolemLevel level = StoneGolemLevel.Level1;

        [SerializeField]
        [Tooltip("Neighbouring anchors reachable in one movement, with the movement type. Edges are " +
                 "auto-mirrored by the navigation graph, so each link only needs wiring on one side.")]
        private AnchorConnection[] connections;

        [SerializeField]
        [Tooltip("Curvature radius of the connection arcs drawn as gizmos (larger = flatter). Clamped " +
                 "to at least half the edge length. Visual only — does not affect movement.")]
        private float connectionArcRadius = 30f;

        /// <summary>The arena level this anchor belongs to.</summary>
        public StoneGolemLevel Level => level;

        /// <summary>World position of the anchor.</summary>
        public Vector2 Position => transform.position;

        /// <summary>Outgoing graph edges authored on this anchor (auto-mirrored by the graph).</summary>
        public IReadOnlyList<AnchorConnection> Connections => connections;

        private void OnDrawGizmos() {
            Gizmos.color = level == StoneGolemLevel.Level1
                ? new Color(0.4f, 0.8f, 1f)
                : new Color(1f, 0.8f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, 0.4f);
            Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, transform.position - Vector3.up * 0.5f);

            DrawConnectionGizmos();
        }

        // Pixel width of the connection lines, and how many segments to sample each arc with.
        private const float ConnectionLineWidth = 4f;
        private const int ConnectionArcSegments = 16;

        // Visualise the navigation edges so the authored graph is verifiable in-scene. Edges are drawn
        // as bowed arcs (see GizmoUtils) so they don't overlap the straight node gizmos or each other.
        private void DrawConnectionGizmos() {
            if (connections == null) {
                return;
            }

            for (int i = 0; i < connections.Length; i++) {
                StoneGolemAnchorPoint target = connections[i].target;
                if (target == null) {
                    continue;
                }

                GizmoUtils.DrawArc(
                    transform.position,
                    target.transform.position,
                    connectionArcRadius,
                    ConnectionColor(connections[i].type),
                    ConnectionLineWidth,
                    ConnectionArcSegments);
            }
        }

        private static Color ConnectionColor(StoneGolemMovementType type) {
            Color color;
            switch (type) {
                case StoneGolemMovementType.Jump: color = Color.green; break;
                case StoneGolemMovementType.HorzJump: color = Color.cyan; break;
                default: color = Color.white; break;
            }

            color.a = 0.7f; // Semi-transparent so overlapping edges and the scene stay readable.
            return color;
        }
    }
}
