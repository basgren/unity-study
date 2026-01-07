namespace Core.Services {
    /// <summary>
    /// Global class for system services. To make it available, add empty game object "System" to
    /// the scene and add SystemInitializer component to it.
    /// For now, when the same is simple, it's enough to use it like global class with constants.
    /// If game gets bigger, it should be reconsidered and migrated to ServiceLocator or DI.
    /// </summary>
    public static class G {
        public static SpawnerService Spawner { get; internal set; }
        public static InputService Input { get; internal set; }
        public static ScreenService Screen { get; internal set; }
        public static GameManager Game { get; internal set; }
        public static StateMachineService StateMachines { get; internal set; }
    }
}
