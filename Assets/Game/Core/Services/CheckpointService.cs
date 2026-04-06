using System;
using System.Collections.Generic;
using Game.Features.Interactive.Bonfire;
using UnityEngine;

namespace Game.Core.Services {
    /// <summary>
    /// Tracks the currently active checkpoint and the set of all discovered checkpoints.
    /// Lives under DontDestroyOnLoad via GInit, so state survives scene loads.
    /// </summary>
    public class CheckpointService : MonoBehaviour {
        public CheckpointRef? Current { get; private set; }

        public event Action<CheckpointRef?> OnCheckpointChanged;

        // Composite key: "{sceneName}:{localId}" — unambiguous across scenes.
        private readonly HashSet<string> discovered = new();

        /// <summary>
        /// When true, the next PlayerController.Start should teleport the player
        /// to the current checkpoint (cross-scene respawn).
        /// </summary>
        public bool HasPendingRespawn { get; private set; }

        public void Activate(CheckpointRef checkpointRef) {
            var key = MakeKey(checkpointRef.Scene.GetSceneName(), checkpointRef.LocalId);
            discovered.Add(key);
            Current = checkpointRef;
            HasPendingRespawn = false;
            OnCheckpointChanged?.Invoke(Current);
        }

        /// <summary>
        /// Returns the visual state of a bonfire in the given scene.
        /// </summary>
        public BonfireState GetBonfireState(string sceneName, string localId) {
            if (Current.HasValue &&
                Current.Value.Scene.GetSceneName() == sceneName &&
                Current.Value.LocalId == localId) {
                return BonfireState.Current;
            }

            if (discovered.Contains(MakeKey(sceneName, localId))) {
                return BonfireState.Discovered;
            }

            return BonfireState.Undiscovered;
        }

        /// <summary>
        /// Marks that the player should respawn at the current checkpoint
        /// after the next scene load.
        /// </summary>
        public void RequestRespawn() {
            HasPendingRespawn = true;
        }

        /// <summary>
        /// Consumes the pending respawn flag and returns the checkpoint reference.
        /// Current must be set before RequestRespawn is called — guaranteed by game flow.
        /// </summary>
        public CheckpointRef ConsumePendingRespawn() {
            HasPendingRespawn = false;
            return Current.Value;
        }

        private static string MakeKey(string sceneName, string localId) {
            return $"{sceneName}:{localId}";
        }
    }
}
