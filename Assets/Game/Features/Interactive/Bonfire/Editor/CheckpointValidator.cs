#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace Game.Features.Interactive.Bonfire.Editor {
    /// <summary>
    /// Editor-only validation for checkpoint IDs.
    /// Validates that each bonfire has a non-empty ID that is unique within its scene.
    /// </summary>
    public static class CheckpointValidator {
        public readonly struct ValidationError {
            public readonly string Message;
            public readonly UnityEngine.Object Context;

            public ValidationError(string message, UnityEngine.Object context) {
                Message = message;
                Context = context;
            }
        }

        /// <summary>
        /// Checks that the given localId is unique within the scene, excluding the specified bonfire.
        /// </summary>
        public static bool IsCheckpointIdUniqueInScene(Scene scene, Bonfire except, string localId) {
            var bonfires = BonfireUtils.GetBonfiresInScene(scene);
            for (var i = 0; i < bonfires.Count; i++) {
                var b = bonfires[i];
                if (b == null || b == except) {
                    continue;
                }

                if (string.Equals(b.CheckpointId, localId, StringComparison.Ordinal)) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Validates all bonfires in the scene: non-empty ID and per-scene uniqueness.
        /// </summary>
        public static List<ValidationError> ValidateScene(Scene scene) {
            var errors = new List<ValidationError>();
            var bonfires = BonfireUtils.GetBonfiresInScene(scene);
            var map = new Dictionary<string, Bonfire>(StringComparer.Ordinal);

            for (var i = 0; i < bonfires.Count; i++) {
                var bonfire = bonfires[i];
                if (bonfire == null) {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(bonfire.CheckpointId)) {
                    errors.Add(new ValidationError(
                        $"Bonfire '{bonfire.name}' has no checkpointId assigned.",
                        bonfire
                    ));
                    continue;
                }

                if (map.TryGetValue(bonfire.CheckpointId, out var other) && other != null) {
                    errors.Add(new ValidationError(
                        $"Duplicate checkpointId '{bonfire.CheckpointId}' in scene '{scene.path}'.",
                        bonfire
                    ));
                } else {
                    map[bonfire.CheckpointId] = bonfire;
                }
            }

            return errors;
        }
    }
}
#endif
