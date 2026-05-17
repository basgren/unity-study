using System.Collections.Generic;

namespace Game.Editor.SceneTopology.Model {
    /// <summary>
    /// Snapshot of all scanned scenes and the resolved portal links between them.
    /// Produced by SceneTopologyScanner, consumed by SceneTopologyLayout and the window view.
    /// </summary>
    public sealed class SceneTopologyGraph {
        public List<SceneNodeData> Nodes = new List<SceneNodeData>();
        public List<PortalEdgeData> Edges = new List<PortalEdgeData>();
        /// <summary>Human-readable issues from scanning: dangling targets, missing confiners, etc.</summary>
        public List<string> Warnings = new List<string>();
        /// <summary>How many <see cref="Nodes"/> were reused from cache vs. freshly scanned. Pure stats.</summary>
        public int CachedNodeCount;
        public int FreshlyScannedNodeCount;
    }
}
