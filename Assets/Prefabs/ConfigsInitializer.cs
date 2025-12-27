using Configs;
using UnityEngine;

namespace Prefabs {
    public class ConfigsInitializer : MonoBehaviour {
        [SerializeField]
        private PlayerConfig playerConfig;
        
        public PlayerConfig PlayerConfig => playerConfig;
    }
}
