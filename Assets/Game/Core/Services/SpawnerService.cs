using Game.Core.Bootstrap;
using Game.Core.Services.Scene;
using Prefabs.Effects.InfoBubble;
using UnityEngine;

namespace Game.Core.Services {
    public class SpawnerService : MonoBehaviour {
        // This object will be created in a scene, or added if not exists.
        private static readonly string VfxParentName = "VFX";
        private static readonly string PropsParentName = "Props";

        private GameObject vfxContainerCache;
        private GameObject propsContainerCache;

        private static AssetRefs assetRefs;
        private static AssetRefs Refs {
            get {
                if (assetRefs == null) {
                    assetRefs = Resources.Load<AssetRefs>("AssetRefs");

                    if (assetRefs == null) {
                        Debug.LogError("AssetCatalog not found. Expected Resources/AssetCatalog.asset");
                    }
                }

                return assetRefs;
            }
        }
        
        private GameObject VfxContainer {
            get {
                if (vfxContainerCache == null) {
                    vfxContainerCache = SceneUtils.GetOrCreateObject(VfxParentName);
                }
                
                return vfxContainerCache;
            }
        }
        
        private GameObject PropsContainer {
            get {
                if (propsContainerCache == null) {
                    propsContainerCache = SceneUtils.GetOrCreateObject(PropsParentName);
                }
                
                return propsContainerCache;
            }
        }

        public GameObject Spawn(GameObject prefab, Vector3 position, Transform parent = null) {
            return Instantiate(prefab, position, Quaternion.identity, parent);
        }

        public GameObject SpawnCollectible(GameObject prefab, Vector3 position) {
            return Instantiate(prefab, position, Quaternion.identity, PropsContainer.transform);
        }

        /// <summary>
        /// Spawns ParticleSystem prefab at position.
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="position"></param>
        /// <returns></returns>
        public ParticleSystem SpawnVfx(ParticleSystem prefab, Vector3 position) {
            return Instantiate(prefab, position, Quaternion.identity, VfxContainer.transform);
        }
        
        public GameObject SpawnVfx(GameObject prefab, Vector3 position, Transform parent = null) {
            if (parent == null) {
                parent = VfxContainer.transform;
            }

            return Instantiate(prefab, position, Quaternion.identity, parent);
        }

        public T SpawnVfx<T>(T prefab, Vector3 position) where T : MonoBehaviour {
            return Instantiate(prefab, position, Quaternion.identity, VfxContainer.transform);
        }

        public void SpawnInfoBubble(InfoBubbleType type, Vector3 position, Transform parent = null, float delay = 1f) {
            var instance = Spawn(Refs.InfoBubblePrefab, position, parent);
            var infoBubble = instance.GetComponent<InfoBubble>();
            infoBubble.ShowBubble(type, delay);
        }
    }
}
