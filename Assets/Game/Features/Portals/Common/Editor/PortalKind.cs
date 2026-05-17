#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace Game.Features.Portals.Common.Editor {
    /// <summary>
    /// Editor-side description of a portal kind (Door, Entrance, ...). Carries the concrete
    /// component type plus the per-kind lookups every generic tool needs.
    /// Created once per kind at editor load and stored by <see cref="PortalKindRegistry"/>.
    /// </summary>
    public sealed class PortalKind {
        public Type ComponentType { get; }
        /// <summary>Human-readable singular noun, e.g. "Door". Used in dropdown labels and messages.</summary>
        public string KindName { get; }
        public Func<Scene, IEnumerable<IPortal>> GetPortalsInScene { get; }
        public Func<Scene, string, IPortal> FindPortalByIdInScene { get; }
        public ScenePortalCache Cache { get; }

        public PortalKind(Type componentType, string kindName,
            Func<Scene, IEnumerable<IPortal>> getPortalsInScene,
            Func<Scene, string, IPortal> findPortalByIdInScene) {
            ComponentType = componentType;
            KindName = kindName;
            GetPortalsInScene = getPortalsInScene;
            FindPortalByIdInScene = findPortalByIdInScene;
            Cache = new ScenePortalCache(this);
        }
    }
}
#endif
