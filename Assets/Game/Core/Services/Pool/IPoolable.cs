namespace Game.Core.Services.Pool {
    /// <summary>
    /// Implement on components that need to react to being acquired from or returned to
    /// an object pool. The lifecycle is driven by <c>SpawnerService.SpawnPooled</c> and
    /// <c>SpawnerService.Despawn</c>.
    /// </summary>
    public interface IPoolable {
        /// <summary>
        /// Called after the instance has been positioned and activated by the pool.
        /// Reset per-spawn runtime state here (counters, flags, transient references).
        /// </summary>
        void OnSpawn();

        /// <summary>
        /// Called just before the instance is deactivated and returned to the pool.
        /// Stop coroutines and detach external references here.
        /// </summary>
        void OnDespawn();
    }
}
