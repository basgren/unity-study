using Game.Core.Bootstrap;
using Game.Core.Components.Base2D;
using Game.Core.Services.Pool;
using UnityEngine;

namespace Game.Core.Components.GameObjects {
    public class SpawnComponent: MonoBehaviour {
        [SerializeField]
        [Tooltip("Where to spawn the prefab. Leave empty to spawn at this object's own transform.")]
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
            // Fall back to this object's own transform when no explicit target is wired,
            // so an unassigned target never throws (e.g. spawning hit debris from a prop).
            var spawnAt = target != null ? target : transform;

            var instance = usePool
                ? G.Spawner.SpawnPooled(prefab, spawnAt.position)
                : G.Spawner.SpawnVfx(prefab, spawnAt.position);

            // As Facing2D is new source of truth, let's use it.
            var myFacing = GetComponent<Facing2D>();
            var spawnedFacing = instance.GetComponent<Facing2D>();

            if (myFacing != null && spawnedFacing != null) {
                spawnedFacing.SetDir(myFacing.Dir);
            } else {
                // Fallback to the old implementation
                // Make sure the spawned object is directed in the same direction as the target object.
                instance.transform.localScale = spawnAt.lossyScale;
            }

            return instance;
        }
    }
}
