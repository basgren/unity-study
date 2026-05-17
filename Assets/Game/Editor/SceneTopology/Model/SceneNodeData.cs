using System.Collections.Generic;
using UnityEngine;

namespace Game.Editor.SceneTopology.Model {
    /// <summary>
    /// One scene's snapshot for the topology view: identity, display name, region grouping,
    /// playable bounds (from CameraConfiner), and the portals found inside.
    /// </summary>
    public sealed class SceneNodeData {
        public string SceneGuid;
        public string ScenePath;
        public string DisplayName;
        /// <summary>
        /// Top-level folder under Assets/Game/Scenes/ (e.g. "DriftwoodShore") or "_root" for ungrouped scenes.
        /// Drives the card tint.
        /// </summary>
        public string RegionKey;
        /// <summary>
        /// World-space AABB of the CameraConfiner polygon, translated so that bounds.min sits at (0,0).
        /// Portals are stored in this local space. When no confiner is found, a default rect is used.
        /// </summary>
        public Rect BoundsLocal;
        /// <summary>True when the scene had no CameraConfiner and BoundsLocal is the fallback rect.</summary>
        public bool BoundsAreFallback;
        public List<PortalData> Portals = new List<PortalData>();
    }
}
