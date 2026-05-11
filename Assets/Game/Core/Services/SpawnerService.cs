using System.Collections.Generic;
using Game.Core.Services.Pool;
using Game.Core.Services.Scene;
using Prefabs.Effects.InfoBubble;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;

namespace Game.Core.Services {
    public class SpawnerService : MonoBehaviour {
        // This object will be created in a scene, or added if not exists.
        private static readonly string VfxParentName = "VFX";
        private static readonly string PropsParentName = "Props";

        private GameObject vfxContainerCache;
        private GameObject propsContainerCache;

        // One pool per prefab reference. Pools hold scene-scoped GameObjects under VfxContainer
        // and are cleared on scene change because the container (and its children) are destroyed.
        private readonly Dictionary<GameObject, ObjectPool<GameObject>> pools = new();

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

        /// <summary>
        /// Spawns a pooled instance of <paramref name="prefab"/>. The prefab root must
        /// carry an <see cref="IPoolable"/> component for the spawn lifecycle to fire.
        /// Position is applied while the instance is still inactive, so <c>OnSpawn</c>
        /// observes the final transform.
        /// </summary>
        public GameObject SpawnPooled(GameObject prefab, Vector3 position) {
            var pool = GetOrCreatePool(prefab);
            var instance = pool.Get();

            // Position must be set before activation so OnEnable/OnSpawn observe the
            // correct transform. Pool's actionOnGet intentionally does NOT activate.
            instance.transform.SetPositionAndRotation(position, Quaternion.identity);
            instance.SetActive(true);

            InvokeOnSpawn(instance);
            return instance;
        }

        public T SpawnPooled<T>(T prefab, Vector3 position) where T : Component {
            var go = SpawnPooled(prefab.gameObject, position);
            return go.GetComponent<T>();
        }

        /// <summary>
        /// Returns a pooled instance to its pool. If the instance has no pool marker
        /// (e.g. it was placed in a scene rather than spawned), falls back to Destroy.
        /// </summary>
        public void Despawn(GameObject instance) {
            if (instance == null) {
                return;
            }

            var marker = instance.GetComponent<PooledInstance>();
            if (marker != null && marker.Pool != null) {
                marker.Pool.Release(instance);
            } else {
                Destroy(instance);
            }
        }

        private void OnEnable() {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnDisable() {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private void OnActiveSceneChanged(UnityEngine.SceneManagement.Scene previous,
            UnityEngine.SceneManagement.Scene next) {
            // Pooled GameObjects live under the scene-scoped VfxContainer and are destroyed
            // when the scene unloads. The pool dictionary would keep stale references —
            // drop everything and let pools rebuild lazily in the new scene.
            pools.Clear();
            vfxContainerCache = null;
            propsContainerCache = null;
        }

        private ObjectPool<GameObject> GetOrCreatePool(GameObject prefab) {
            if (pools.TryGetValue(prefab, out var existing)) {
                return existing;
            }

            // Closure captures `created` so the marker added in createFunc can hold its pool back-reference.
            ObjectPool<GameObject> created = null;
            created = new ObjectPool<GameObject>(
                createFunc: () => CreatePooledInstance(prefab, created),
                actionOnGet: null,
                actionOnRelease: OnReturnToPool,
                actionOnDestroy: OnDestroyPoolItem,
                collectionCheck: true,
                defaultCapacity: 8,
                maxSize: 64
            );

            pools[prefab] = created;
            return created;
        }

        private GameObject CreatePooledInstance(GameObject prefab, ObjectPool<GameObject> ownerPool) {
            var instance = Instantiate(prefab, VfxContainer.transform);

            var marker = instance.AddComponent<PooledInstance>();
            marker.Pool = ownerPool;
            marker.Poolables = instance.GetComponentsInChildren<IPoolable>(includeInactive: true);

            instance.SetActive(false);
            return instance;
        }

        private static void InvokeOnSpawn(GameObject instance) {
            var marker = instance.GetComponent<PooledInstance>();
            if (marker == null || marker.Poolables == null) {
                return;
            }

            for (int i = 0; i < marker.Poolables.Length; i++) {
                marker.Poolables[i].OnSpawn();
            }
        }

        private static void OnReturnToPool(GameObject instance) {
            var marker = instance.GetComponent<PooledInstance>();
            if (marker != null && marker.Poolables != null) {
                for (int i = 0; i < marker.Poolables.Length; i++) {
                    marker.Poolables[i].OnDespawn();
                }
            }

            instance.SetActive(false);
        }

        private static void OnDestroyPoolItem(GameObject instance) {
            Destroy(instance);
        }
    }
}
