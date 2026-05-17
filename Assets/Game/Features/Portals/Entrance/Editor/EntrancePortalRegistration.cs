#if UNITY_EDITOR
using Game.Features.Portals.Common;
using Game.Features.Portals.Common.Editor;
using UnityEditor;

namespace Game.Features.Portals.Entrance.Editor {
    /// <summary>
    /// Registers the Entrance kind with <see cref="PortalKindRegistry"/> so the shared editor tools
    /// (link drawer, validator, project updater, change-id window, scene-reference repair) can
    /// resolve Entrance without Common code referencing the concrete type.
    /// </summary>
    [InitializeOnLoad]
    public static class EntrancePortalRegistration {
        static EntrancePortalRegistration() {
            PortalKindRegistry.Register(new PortalKind(
                typeof(Entrance),
                "Entrance",
                scene => PortalUtils.GetPortalsInScene<Entrance>(scene),
                (scene, id) => PortalUtils.FindPortalByIdInScene<Entrance>(scene, id)
            ));
        }
    }
}
#endif
