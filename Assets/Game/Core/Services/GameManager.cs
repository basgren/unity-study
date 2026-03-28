using Game.Configs;
using Game.Features.Characters.Hero;
using UnityEngine;

namespace Game.Core.Services {
    public class GameManager : MonoBehaviour {
        public PlayerConfig playerConfig;

        [SerializeField]
        public PlayerState playerState;

        public void Init() {
            ResetPlayerState();
        }

        public void ResetPlayerState() {
            playerState = new PlayerState(playerConfig);
        }
    }
}
