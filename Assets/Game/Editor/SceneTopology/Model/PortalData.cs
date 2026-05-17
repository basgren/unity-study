using UnityEngine;

namespace Game.Editor.SceneTopology.Model {
    /// <summary>
    /// One portal (Door or Entrance) discovered inside a scanned scene.
    /// Positions are stored relative to the scene's bounds origin (bounds.min), so they can be
    /// rendered directly inside a normalized scene card.
    /// </summary>
    public sealed class PortalData {
        public string Id;
        public PortalKindRef Kind;
        /// <summary>Position inside the scene card, in scene world units relative to bounds.min.</summary>
        public Vector2 LocalPos;
        public string TargetSceneGuid;
        public string TargetId;
    }
}
