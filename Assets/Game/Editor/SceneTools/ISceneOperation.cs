#if UNITY_EDITOR
using UnityEngine.SceneManagement;

namespace Game.Editor.SceneTools {
    /// <summary>
    /// A single maintenance operation that can run on a scene (validation or repair).
    /// Implementations are discovered automatically by <see cref="SceneToolsWindow"/> via TypeCache
    /// and must have a public parameterless constructor.
    /// </summary>
    public interface ISceneOperation {
        /// <summary>Grouping label shown in the operation dropdown (e.g. "Scene State").</summary>
        string Category { get; }

        /// <summary>Human-readable operation name (e.g. "Validate StateRoot Ids").</summary>
        string DisplayName { get; }

        /// <summary>
        /// True if the operation can modify the scene. When true and
        /// <see cref="SceneOperationResult.Changes"/> is greater than zero, the runner marks the
        /// scene dirty and saves it.
        /// </summary>
        bool Mutates { get; }

        /// <summary>
        /// Runs the operation on a loaded scene, reporting findings through <paramref name="log"/>.
        /// </summary>
        SceneOperationResult Run(Scene scene, ISceneOperationLog log);
    }
}
#endif
