#if UNITY_EDITOR
namespace Game.Features.Portals.Common.Editor {
    /// <summary>
    /// Dropdown row describing a portal found in a scene. Used by <c>ScenePortalCache</c>
    /// and rendered by <c>PortalLinkDrawer</c>.
    /// </summary>
    public readonly struct PortalEntry {
        public readonly string Id;
        public readonly string ObjectName;
        public readonly string HierarchyPath;

        public PortalEntry(string id, string objectName, string hierarchyPath) {
            Id = id;
            ObjectName = objectName;
            HierarchyPath = hierarchyPath;
        }
    }
}
#endif
