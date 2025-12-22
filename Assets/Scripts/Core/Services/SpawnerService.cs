using UnityEngine;

namespace Core.Services {
    public class SpawnerService : MonoBehaviour {
        // This object will be created in a scene, or added if not exists.
        private static readonly string VfxParentName = "VFX";
        private static readonly string PropsParentName = "Props";

        private GameObject vfxContainer;
        private GameObject propsContainer;

        private void Awake() {
            vfxContainer = GetOrCreate(VfxParentName);
            propsContainer = GetOrCreate(PropsParentName);
        }

        public GameObject Spawn(GameObject prefab, Vector3 position) {
            return Instantiate(prefab, position, Quaternion.identity);
        }

        public GameObject SpawnCollectible(GameObject prefab, Vector3 position) {
            return Instantiate(prefab, position, Quaternion.identity, propsContainer.transform);
        }

        /// <summary>
        /// Spawns ParticleSystem prefab at position.
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        public ParticleSystem SpawnVfx(ParticleSystem prefab, Vector3 position) {
            return Instantiate(prefab, position, Quaternion.identity, vfxContainer.transform);
        }
        
        public GameObject SpawnVfx(GameObject prefab, Vector3 position, Transform parent = null) {
            if (parent == null) {
                parent = vfxContainer.transform;
            }

            return Instantiate(prefab, position, Quaternion.identity, parent);
        }

        public T SpawnVfx<T>(T prefab, Vector3 position) where T : MonoBehaviour {
            return Instantiate(prefab, position, Quaternion.identity, vfxContainer.transform);
        }

        private GameObject GetOrCreate(string gameObjectName) {
            return GameObject.Find(gameObjectName) ?? new GameObject(gameObjectName);
        }
    }
}
