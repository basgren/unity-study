#if UNITY_EDITOR
using Game.Features.Portals.Common;
using Game.Features.Portals.Common.Editor;
using UnityEditor;

namespace Game.Features.Portals.Doors.Editor {
    /// <summary>
    /// Registers the Door kind with <see cref="PortalKindRegistry"/> so the shared editor tools
    /// (link drawer, validator, project updater, change-id window, scene-reference repair) can
    /// resolve Door without Common code referencing the concrete type.
    /// </summary>
    [InitializeOnLoad]
    public static class DoorPortalRegistration {
        static DoorPortalRegistration() {
            PortalKindRegistry.Register(new PortalKind(
                typeof(Door),
                "Door",
                scene => PortalUtils.GetPortalsInScene<Door>(scene),
                (scene, id) => PortalUtils.FindPortalByIdInScene<Door>(scene, id)
            ));
        }
    }
}
#endif
