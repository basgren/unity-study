#if UNITY_EDITOR
namespace Game.Editor.SceneTools {
    /// <summary>
    /// Outcome of running an <see cref="ISceneOperation"/> on one scene.
    /// </summary>
    public readonly struct SceneOperationResult {
        /// <summary>Number of problems found (validators). Zero for a clean scene.</summary>
        public readonly int Issues;

        /// <summary>Number of mutations made (fixers). When greater than zero the runner saves the scene.</summary>
        public readonly int Changes;

        public SceneOperationResult(int issues, int changes) {
            Issues = issues;
            Changes = changes;
        }

        public static SceneOperationResult Validation(int issues) {
            return new SceneOperationResult(issues, 0);
        }

        public static SceneOperationResult Fix(int changes) {
            return new SceneOperationResult(0, changes);
        }
    }
}
#endif
