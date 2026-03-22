using Core.Audio;
using Game.Configs;
using UnityEngine;

namespace Core.Services {
    /// <summary>
    /// This component should be added to the scene to initialize the system. It initializes
    /// global service class G.
    /// </summary>
    public class GInit : MonoBehaviour {
        private static readonly string MainConfigResourcePath = "Configs/MainConfig";
        private MainConfig mainConfig;
        
        private void Awake() {
            if (HasExistingInitializers()) {
                DestroyImmediate(gameObject);
                return;
            }

            LoadConfig();
            G.Config = mainConfig;

            Debug.Log("Initializing Game Manager");
            G.Game = GetOrCreate<GameManager>("GameManager");
            
            G.Spawner = GetOrCreate<SpawnerService>("SpawnerService");
            G.Input = GetOrCreate<InputService>("InputService");
            G.Screen = GetOrCreate<ScreenService>("ScreenService");
            G.StateMachines = GetOrCreate<StateMachineService>("StateMachineService");
            G.Audio = GetOrCreate<AudioService>("AudioService");
            G.Menu = GetOrCreate<MenuManager>("MenuManager");
            G.Game.playerConfig = mainConfig.Player;
            G.Game.Init();
        }

        private void LoadConfig() {
            MainConfig config = Resources.Load<MainConfig>(MainConfigResourcePath);
            if (config == null) {
                Debug.LogError($"Failed to load config from '{MainConfigResourcePath}'");
            }
            
            Debug.Log("Setting main config");
            mainConfig = config;
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
            var inits = FindObjectsOfType<GInit>();
            foreach (var sysInit in inits) {
                if (sysInit != this) {
                    return true;
                }
            }
            
            return false;
        }
    }
}
