using UnityEngine;

namespace Game.Configs {
    public class ConfigsInitializer : MonoBehaviour {
        [SerializeField]
        private PlayerConfig playerConfig;
        
        public PlayerConfig PlayerConfig => playerConfig;
    }
}
