using System;
using Game.Core.Services.Scene;
using UnityEngine;

namespace Game.Doors {
    /// <summary>
    /// A link that identifies the destination door in a destination scene.
    /// </summary>
    [Serializable]
    public struct DoorLink {
        [SerializeField]
        private SceneReference targetScene;

        [SerializeField]
        private string targetDoorId;

        /// <summary>
        /// Destination scene reference (GUID-based).
        /// </summary>
        public SceneReference TargetScene => targetScene;

        /// <summary>
        /// Destination door id inside the target scene.
        /// </summary>
        public string TargetDoorId => targetDoorId;
    }
}
