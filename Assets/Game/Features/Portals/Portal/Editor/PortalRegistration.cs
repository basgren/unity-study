#if UNITY_EDITOR
using Game.Features.Portals.Common;
using Game.Features.Portals.Common.Editor;
using UnityEditor;

namespace Game.Features.Portals.Portal.Editor {
    /// <summary>
    /// Registers the Portal kind with <see cref="PortalKindRegistry"/> so the shared editor tools
    /// (link drawer, validator, change-id window, scene-reference repair) resolve Portal without
    /// Common code referencing the concrete type.
    /// </summary>
    [InitializeOnLoad]
    public static class PortalRegistration {
        static PortalRegistration() {
            PortalKindRegistry.Register(new PortalKind(
                typeof(Portal),
                "Portal",
                scene => PortalUtils.GetPortalsInScene<Portal>(scene),
                (scene, id) => PortalUtils.FindPortalByIdInScene<Portal>(scene, id)
            ));
        }
    }
}
#endif
