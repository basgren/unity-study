using Core.Components.Base2D;
using Game.Core.Bootstrap;
using Game.Core.Services.Pool;
using UnityEngine;

namespace Game.Core.Components.GameObjects {
    public class SpawnComponent: MonoBehaviour {
        [SerializeField]
        private Transform target;

        [SerializeField]
        private GameObject prefab;

        private bool usePool;

        private void Awake() {
            // Auto-route through the pool when the prefab opts in by implementing IPoolable.
            usePool = prefab != null && prefab.GetComponent<IPoolable>() != null;
        }

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
            var instance = usePool
                ? G.Spawner.SpawnPooled(prefab, target.position)
                : G.Spawner.SpawnVfx(prefab, target.position);

            // As Facing2D is new source of truth, let's use it.
            var myFacing = GetComponent<Facing2D>();
            var spawnedFacing = instance.GetComponent<Facing2D>();

            if (myFacing != null && spawnedFacing != null) {
                spawnedFacing.SetDir(myFacing.Dir);
            } else {
                // Fallback to the old implementation
                // Make sure the spawned object is directed in the same direction as the target object.
                instance.transform.localScale = target.lossyScale;
            }

            return instance;
        }
    }
}
