using Game.Core.Services.Scene;
using UnityEngine;

namespace Game.Features.Portals.Common {
    /// <summary>
    /// Shared contract for scene portals (Doors, Entrances, ...) used by <see cref="PortalTravelService"/>.
    /// Portals expose a stable identifier, a destination (scene + portal id) and a hook to be invoked when
    /// the player has entered the portal.
    /// </summary>
    public interface IPortal {
        /// <summary>
        /// Stable identifier of this portal within its scene.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Destination scene.
        /// </summary>
        SceneReference TargetScene { get; }

        /// <summary>
        /// Destination portal id inside the target scene. Must reference a portal of the same kind.
        /// </summary>
        string TargetId { get; }

        /// <summary>
        /// Position the player should be teleported to when arriving at this portal.
        /// </summary>
        Vector3 GetEntryPosition();

        /// <summary>
        /// Called after the player has arrived at this portal (e.g. close animation, sfx hooks).
        /// </summary>
        void NotifyEntered();

        /// <summary>
        /// Backing GameObject (needed to look up the owning scene).
        /// </summary>
        GameObject gameObject { get; }
    }
}
