using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core.Services.Pool {
    /// <summary>
    /// Marker attached to every pooled GameObject. Holds the back-reference to its
    /// owning pool so <c>SpawnerService.Despawn</c> can return the instance, and
    /// caches all <see cref="IPoolable"/> components on the instance to avoid
    /// allocating GetComponents results on every spawn/release.
    /// </summary>
    public class PooledInstance : MonoBehaviour {
        public ObjectPool<GameObject> Pool { get; set; }
        public IPoolable[] Poolables { get; set; }
    }
}
