using Configs;
using Game;
using UnityEngine;

namespace Core.Services {
    public class GameManager : MonoBehaviour {
        public PlayerConfig playerConfig;
        public PlayerState PlayerState { get; private set; }

        public void Init() {
            ResetPlayerState();
        }
        
        public void ResetPlayerState() {
            PlayerState = new PlayerState(playerConfig);
        }
    }
}
