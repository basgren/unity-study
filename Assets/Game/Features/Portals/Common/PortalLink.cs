using System;
using Game.Core.Services.Scene;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Features.Portals.Common {
    /// <summary>
    /// Destination of a portal: target scene + id of a portal inside that scene.
    /// One struct shared by every portal kind (Door, Entrance, ...).
    /// The owning component decides which kind of portal the id must reference; the editor
    /// drawer filters the dropdown accordingly via <c>PortalKindRegistry</c>.
    /// </summary>
    [Serializable]
    public struct PortalLink {
        [SerializeField]
        private SceneReference targetScene;

        // Older Door/Entrance serialized data used these field names; FormerlySerializedAs migrates
        // them into `targetId` on first scene load after this refactor.
        [SerializeField]
        private string targetId;

        public SceneReference TargetScene => targetScene;

        /// <summary>
        /// Destination portal id inside the target scene. Stored as a numeric string ("1", "2", ...);
        /// empty means unassigned.
        /// </summary>
        public string TargetId => targetId;
    }
}
