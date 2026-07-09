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

        /// <summary>
        /// True while a bonfire rest reload is in progress.
        /// The freshly loaded player should stay locked and invulnerable until the transition completes.
        /// </summary>
        public bool IsBonfireRestTransitionActive { get; private set; }

        public void Activate(CheckpointRef checkpointRef) {
            var key = MakeKey(checkpointRef.Scene.GetSceneName(), checkpointRef.LocalId);
            discovered.Add(key);
            Current = checkpointRef;
            HasPendingRespawn = false;
            OnCheckpointChanged?.Invoke(Current);
        }

        /// <summary>
        /// Fully clears all checkpoint progress. Used when starting a new game.
        /// Forgets the active checkpoint and every discovered bonfire and cancels any pending respawn.
        /// </summary>
        public void Reset() {
            Current = null;
            discovered.Clear();
            HasPendingRespawn = false;
            IsBonfireRestTransitionActive = false;
            OnCheckpointChanged?.Invoke(Current);
        }

        /// <summary>
        /// All discovered checkpoint keys ("{sceneName}:{localId}"). Exposed for persistence.
        /// </summary>
        public IReadOnlyCollection<string> DiscoveredKeys => discovered;

        /// <summary>
        /// Replaces all checkpoint state from a loaded save: the active checkpoint (or null)
        /// and the set of discovered bonfires. Cancels any pending respawn/transition and
        /// notifies listeners so bonfire visuals refresh.
        /// </summary>
        public void RestoreState(CheckpointRef? current, IEnumerable<string> discoveredKeys) {
            discovered.Clear();
            if (discoveredKeys != null) {
                foreach (var key in discoveredKeys) {
                    discovered.Add(key);
                }
            }

            Current = current;
            HasPendingRespawn = false;
            IsBonfireRestTransitionActive = false;
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
            IsBonfireRestTransitionActive = false;
        }

        /// <summary>
        /// Marks that the next scene load comes from a bonfire rest transition.
        /// Use this instead of <see cref="RequestRespawn"/> when a fade sequence will restore
        /// player controls and vulnerability only after the fade-in completes.
        /// </summary>
        public void BeginBonfireRestTransition() {
            HasPendingRespawn = true;
            IsBonfireRestTransitionActive = true;
        }

        /// <summary>
        /// Consumes the pending respawn flag and returns the checkpoint reference.
        /// Current must be set before RequestRespawn is called — guaranteed by game flow.
        /// </summary>
        public CheckpointRef ConsumePendingRespawn() {
            HasPendingRespawn = false;
            return Current.Value;
        }

        /// <summary>
        /// Ends the bonfire rest transition after the new scene has faded back in and the player
        /// can safely regain control.
        /// </summary>
        public void CompleteBonfireRestTransition() {
            IsBonfireRestTransitionActive = false;
        }

        private static string MakeKey(string sceneName, string localId) {
            return $"{sceneName}:{localId}";
        }
    }
}
