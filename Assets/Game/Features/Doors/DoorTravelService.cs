using Game.Features.Portals;

namespace Game.Features.Doors {
    /// <summary>
    /// Thin wrapper around <see cref="PortalTravelService"/> specialized for doors.
    /// Kept as a separate entry point because the door prefab wires UnityEvents to <see cref="Door.TravelToTarget"/>,
    /// which in turn delegates here. Do not inline into Door without updating prefab event bindings.
    /// </summary>
    public static class DoorTravelService {
        public static void Travel(Door fromDoor) {
            PortalTravelService.Travel(fromDoor, FindDoor);
        }

        private static IPortal FindDoor(UnityEngine.SceneManagement.Scene scene, string doorId) {
            return DoorUtils.FindDoorByIdInScene(scene, doorId);
        }
    }
}
