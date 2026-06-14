#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game.Core.Services.SceneState;
using UnityEngine.SceneManagement;

namespace Game.Editor.SceneState {
    /// <summary>
    /// Editor-only validation for StateRoot save IDs.
    /// Each saveable StateRoot must have a non-empty saveId that is unique within its scene,
    /// otherwise two objects share one save slot and overwrite each other's state.
    /// Roots flagged SkipSave are ignored: they are excluded from capture/restore and may
    /// legitimately share an id (or have none).
    /// </summary>
    public static class StateRootValidator {
        public readonly struct ValidationError {
            public readonly string Message;
            public readonly UnityEngine.Object Context;

            public ValidationError(string message, UnityEngine.Object context) {
                Message = message;
                Context = context;
            }
        }

        /// <summary>
        /// Validates all saveable StateRoots in the scene: non-empty saveId and per-scene uniqueness.
        /// </summary>
        public static List<ValidationError> ValidateScene(Scene scene) {
            var errors = new List<ValidationError>();
            if (!scene.IsValid()) {
                return errors;
            }

            // First occurrence of each id, used to point the duplicate error at the original too.
            var seen = new Dictionary<string, StateRoot>(StringComparer.Ordinal);

            foreach (var root in GetSaveableRootsInScene(scene)) {
                if (string.IsNullOrEmpty(root.SaveId)) {
                    errors.Add(new ValidationError(
                        $"StateRoot on '{root.name}' has no saveId in scene '{scene.name}'.",
                        root
                    ));
                    continue;
                }

                if (seen.TryGetValue(root.SaveId, out var original)) {
                    errors.Add(new ValidationError(
                        $"Duplicate StateRoot saveId '{root.SaveId}' in scene '{scene.name}': " +
                        $"'{root.name}' collides with '{original.name}'.",
                        root
                    ));
                } else {
                    seen[root.SaveId] = root;
                }
            }

            return errors;
        }

        private static List<StateRoot> GetSaveableRootsInScene(Scene scene) {
            var result = new List<StateRoot>();
            foreach (var go in scene.GetRootGameObjects()) {
                foreach (var root in go.GetComponentsInChildren<StateRoot>(true)) {
                    if (!root.SkipSave) {
                        result.Add(root);
                    }
                }
            }

            return result;
        }
    }
}
#endif
