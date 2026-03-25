using Game;
using Game.Configs;
using Game.Models;
using UnityEngine;

namespace Core.Services {
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
