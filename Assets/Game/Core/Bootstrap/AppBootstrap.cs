using UnityEngine;

namespace Game.Core.Bootstrap {
    public static class AppBootstrap {
        private static readonly string EntryPointObjectName = "EntryPoint";
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init() {
            // Prevent duplicates (domain reload on/off, etc.)
            var existing = Object.FindAnyObjectByType<GInit>();
            if (existing != null) {
                Debug.Log("Already initialized.");
                return;
            }
            
            var go = new GameObject(EntryPointObjectName);
            Object.DontDestroyOnLoad(go);
            go.AddComponent<GInit>();
        }
    }
}
