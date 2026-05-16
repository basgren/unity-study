using Game.Features.Portals;

namespace Game.Features.Doors {
    /// <summary>
    /// Thin wrapper around <see cref="PortalTravelService"/> specialized for entrances.
    /// Entrances connect only to other entrances; the finder used here looks up the
    /// destination by entrance id only.
    /// </summary>
    public static class EntranceTravelService {
        public static void Travel(Entrance fromEntrance) {
            PortalTravelService.Travel(fromEntrance, FindEntrance);
        }

        private static IPortal FindEntrance(UnityEngine.SceneManagement.Scene scene, string entranceId) {
            return EntranceUtils.FindEntranceByIdInScene(scene, entranceId);
        }
    }
}
