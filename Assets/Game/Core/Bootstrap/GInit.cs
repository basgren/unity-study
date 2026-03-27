using Game.Configs;
using Game.Core.Audio;
using Game.Core.Services;
using Game.Core.Services.Input;
using Game.Core.Services.Scene;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core.Bootstrap {
    /// <summary>
    /// This component should be added to the scene to initialize the system. It initializes
    /// global service class G.
    /// </summary>
    public class GInit : MonoBehaviour {
        private static readonly string MainConfigResourcePath = "MainConfig";
        private MainConfig mainConfig;

        private void Awake() {
            if (HasExistingInitializers()) {
                DestroyImmediate(gameObject);
                return;
            }

            LoadConfig();
            G.Config = mainConfig;

            EnsureEventSystem();
            
            Debug.Log("Initializing Game Manager");
            G.Game = GetOrCreate<GameManager>("GameManager");
            G.Hero = GetOrCreate<HeroService>("HeroService");
            G.Spawner = GetOrCreate<SpawnerService>("SpawnerService");
            G.Input = GetOrCreate<InputService>("InputService");
            G.Screen = GetOrCreate<ScreenService>("ScreenService");
            G.StateMachines = GetOrCreate<StateMachineService>("StateMachineService");
            G.Settings = GetOrCreate<SettingsService>("SettingsService");
            var audioService = GetOrCreate<AudioService>("AudioService");
            G.Audio = audioService;
            G.Menu = GetOrCreate<MenuManager>("MenuManager");
            G.Hud = GetOrCreate<HudService>("HudService");
            G.Game.playerConfig = mainConfig.Player;
            G.Game.Init();

            audioService.Init();
            G.Settings.Init();
            G.Hud.Init();
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

        private void EnsureEventSystem() {
            EventSystem existing = FindFirstObjectByType<EventSystem>();

            if (existing != null) {
                return;
            }

            GameObject instance = Instantiate(mainConfig.eventSystem.gameObject);
            DontDestroyOnLoad(instance);
        }
    }
}
