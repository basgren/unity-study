using Core.Services;
using UnityEngine;

namespace Core.Components.GameObjects {
    public class SpawnComponent: MonoBehaviour {
        [SerializeField]
        private Transform target;

        [SerializeField]
        private GameObject prefab;

        [ContextMenu("Spawn")]
        public void Spawn() {
            SpawnInstance();
        }
        
        public GameObject SpawnInstance() {
            var instance = G.Spawner.SpawnVfx(prefab, target.position);
            
            // Make sure the spawned object is directed in the same direction as the target object.
            instance.transform.localScale = target.lossyScale;
            
            return instance;
        }
    }
}
