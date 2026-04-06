using Game.Features.Doors;

namespace Game.Core.Services {
    /// <summary>
    /// Stable composite reference to a checkpoint (bonfire) in the game world.
    /// Identifies a checkpoint by scene and a local ID that is unique within that scene.
    /// </summary>
    [System.Serializable]
    public struct CheckpointRef {
        public SceneReference Scene;
        public string LocalId;
    }
}
