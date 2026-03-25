using Core.Services;
using UnityEngine;

namespace System {
    public static class AppBootstrap {
        private static readonly string EntryPointObjectName = "EntryPoint";
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init() {
            Debug.Log(">>>> App Bootstrap");
            // Prevent duplicates (domain reload on/off, etc.)
            var existing = UnityEngine.Object.FindObjectOfType<GInit>();
            if (existing != null) {
                Debug.Log("Already initialized.");
                return;
            }
            
            var go = new GameObject(EntryPointObjectName);
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<GInit>();
        }
    }
}
