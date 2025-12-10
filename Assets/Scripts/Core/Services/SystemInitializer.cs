using UnityEngine;

namespace Core.Services {
    /// <summary>
    /// This component should be added to the scene to initialize the system. It initializes
    /// global service class G.
    /// </summary>
    public class SystemInitializer : MonoBehaviour {
        private void Awake() {
            // DontDestroyOnLoad(gameObject); 

            G.Spawner = GetOrCreate<SpawnerService>("SpawnerService");
            G.Input = GetOrCreate<InputService>("InputService");
            // G.Audio = GetOrCreate<AudioService>("AudioService");
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
    }
}
