namespace Game.Editor.SceneTopology.Model {
    public enum PortalKindRef {
        Entrance,
        Door,
        // Appended (not reordered): the on-disk cache and PortalKey both serialize this as (int).
        EntranceHorizontal,
    }
}
