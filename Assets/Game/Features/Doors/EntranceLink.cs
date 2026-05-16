using System;
using Game.Core.Services.Scene;
using UnityEngine;

namespace Game.Features.Doors {
    /// <summary>
    /// A link that identifies the destination entrance in a destination scene.
    /// Entrances connect only to entrances; this is enforced by validation and editor tooling.
    /// </summary>
    [Serializable]
    public struct EntranceLink {
        [SerializeField]
        private SceneReference targetScene;

        [SerializeField]
        private string targetEntranceId;

        /// <summary>
        /// Destination scene reference (GUID-based).
        /// </summary>
        public SceneReference TargetScene => targetScene;

        /// <summary>
        /// Destination entrance id inside the target scene.
        /// </summary>
        public string TargetEntranceId => targetEntranceId;
    }
}
