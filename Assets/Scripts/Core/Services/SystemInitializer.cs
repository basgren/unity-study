using Prefabs;
using UnityEngine;

namespace Core.Services {
    /// <summary>
    /// This component should be added to the scene to initialize the system. It initializes
    /// global service class G.
    /// </summary>
    public class SystemInitializer : MonoBehaviour {
        private void Awake() {
            if (HasExistingInitializers()) {
                DestroyImmediate(gameObject);
                return;
            }
            
            DontDestroyOnLoad(gameObject);
            
            
            
            Debug.Log("Initializing Game Manager");
            G.Game = GetOrCreate<GameManager>("GameManager");
            G.Spawner = GetOrCreate<SpawnerService>("SpawnerService");
            G.Input = GetOrCreate<InputService>("InputService");
            G.Screen = GetOrCreate<ScreenService>("ScreenService");
            // G.Audio = GetOrCreate<AudioService>("AudioService");

            // TODO: [BG] think about better way to bind configs. bootstrap room?
            var configs = GetComponent<ConfigsInitializer>();
            G.Game.playerConfig = configs.PlayerConfig;
            G.Game.Init();
        }

        private T GetOrCreate<T>(string serviceName) where T : MonoBehaviour {
            T svc = GetComponentInChildren<T>();
            if (svc != null) {
                
                return svc;
            }

            GameObject go = new GameObject(serviceName);
            go.transform.SetParent(transform);
            return go.AddComponent<T>();
        }

        private bool HasExistingInitializers() {
            var inits = FindObjectsOfType<SystemInitializer>();
            foreach (var sysInit in inits) {
                if (sysInit != this) {
                    return true;
                }
            }
            
            return false;
        }
    }
}
