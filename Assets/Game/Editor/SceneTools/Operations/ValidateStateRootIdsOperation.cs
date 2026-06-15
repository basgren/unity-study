#if UNITY_EDITOR
using Game.Editor.SceneState;
using UnityEngine.SceneManagement;

namespace Game.Editor.SceneTools.Operations {
    /// <summary>
    /// Read-only operation: validates StateRoot save-id presence and per-scene uniqueness.
    /// Wraps <see cref="StateRootValidator"/>.
    /// </summary>
    public sealed class ValidateStateRootIdsOperation : ISceneOperation {
        public string Category => "Scene State";
        public string DisplayName => "Validate StateRoot Ids";
        public bool Mutates => false;

        public SceneOperationResult Run(Scene scene, ISceneOperationLog log) {
            var errors = StateRootValidator.ValidateScene(scene);
            foreach (var error in errors) {
                log.Error(error.Message, error.Context);
            }

            if (errors.Count == 0) {
                log.Info("StateRoot ids OK.");
            }

            return SceneOperationResult.Validation(errors.Count);
        }
    }
}
#endif
