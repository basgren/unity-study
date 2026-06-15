#if UNITY_EDITOR
using Game.Editor.SceneState;
using UnityEngine.SceneManagement;

namespace Game.Editor.SceneTools.Operations {
    /// <summary>
    /// Repair operation: reassigns fresh unique save-ids to duplicate StateRoots. Saving the scene
    /// (done by the runner when changes were made) additionally heals missing ids via the on-save
    /// hook. Wraps <see cref="StateRootIdAssigner.ReassignDuplicateIds"/>.
    /// </summary>
    public sealed class FixStateRootIdsOperation : ISceneOperation {
        public string Category => "Scene State";
        public string DisplayName => "Fix Duplicate StateRoot Ids";
        public bool Mutates => true;

        public SceneOperationResult Run(Scene scene, ISceneOperationLog log) {
            var fixedCount = StateRootIdAssigner.ReassignDuplicateIds(scene);
            if (fixedCount > 0) {
                log.Info($"Reassigned {fixedCount} duplicate StateRoot id(s).");
            } else {
                log.Info("No duplicate StateRoot ids.");
            }

            return SceneOperationResult.Fix(fixedCount);
        }
    }
}
#endif
