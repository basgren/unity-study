using System;
using Game.Configs;
using Game.Features.Characters.Hero;
using UnityEngine;

namespace Game.Core.Services {
    public class GameManager : MonoBehaviour {
        public PlayerConfig playerConfig;

        [SerializeField]
        public PlayerState playerState;

        /// <summary>
        /// Raised whenever the current <see cref="playerState"/> instance is replaced
        /// (fresh game or loaded save). Consumers that cache references into the player
        /// state (e.g. the save service's inventory hook) must re-bind on this event.
        /// </summary>
        public event Action PlayerStateChanged;

        public void Init() {
            ResetPlayerState();
        }

        public void ResetPlayerState() {
            playerState = new PlayerState(playerConfig);
            PlayerStateChanged?.Invoke();
        }

        /// <summary>
        /// Replaces the current player state with a deserialized one (used when loading a
        /// saved game). The caller is responsible for having rebuilt any transient models.
        /// </summary>
        public void SetPlayerState(PlayerState state) {
            playerState = state;
            PlayerStateChanged?.Invoke();
        }
    }
}
