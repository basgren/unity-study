namespace Game.Editor.SceneTopology.Model {
    /// <summary>
    /// A resolved portal-to-portal link between two scanned scenes. Same-scene Door-Door edges
    /// are dropped at edge build time, so any edge present here is renderable.
    /// </summary>
    public sealed class PortalEdgeData {
        public string FromSceneGuid;
        public string FromPortalId;
        public string ToSceneGuid;
        public string ToPortalId;
        public PortalKindRef Kind;
    }
}
