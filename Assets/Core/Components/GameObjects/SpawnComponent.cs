using Core.Services;
using UnityEngine;

namespace Core.Components.GameObjects {
    public class SpawnComponent: MonoBehaviour {
        [SerializeField]
        private Transform target;

        [SerializeField]
        private GameObject prefab;

        /// <summary>
        /// Spawns a prefab at the target position. Returns void and should be used when it needs just to be spawned,
        /// for example from context menu or action callback.
        /// </summary>
        [ContextMenu("Spawn")]
        public void Spawn() {
            SpawnInstance();
        }
        
        /// <summary>
        /// Use this method to spawn and return an actual instance of the prefab.
        /// </summary>
        /// <returns></returns>
        public GameObject SpawnInstance() {
            var instance = G.Spawner.SpawnVfx(prefab, target.position);
            
            // Make sure the spawned object is directed in the same direction as the target object.
            instance.transform.localScale = target.lossyScale;
            
            return instance;
        }
    }
}
