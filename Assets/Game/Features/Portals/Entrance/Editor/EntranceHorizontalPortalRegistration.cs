#if UNITY_EDITOR
using Game.Features.Portals.Common;
using Game.Features.Portals.Common.Editor;
using UnityEditor;

namespace Game.Features.Portals.Entrance.Editor {
    /// <summary>
    /// Registers the EntranceHorizontal kind with <see cref="PortalKindRegistry"/> so the shared
    /// editor tools (link drawer, validator, project updater, change-id window, scene-reference repair)
    /// can resolve it without Common code referencing the concrete type.
    /// </summary>
    [InitializeOnLoad]
    public static class EntranceHorizontalPortalRegistration {
        static EntranceHorizontalPortalRegistration() {
            PortalKindRegistry.Register(new PortalKind(
                typeof(EntranceHorizontal),
                "EntranceHorizontal",
                scene => PortalUtils.GetPortalsInScene<EntranceHorizontal>(scene),
                (scene, id) => PortalUtils.FindPortalByIdInScene<EntranceHorizontal>(scene, id)
            ));
        }
    }
}
#endif
