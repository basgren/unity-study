#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game.Core.Utils;
using Game.Doors.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Features.Doors.Editor {
    /// <summary>
    /// Editor-only validation for entrance ids and entrance links.
    /// Used by menu validation and play-mode validation.
    /// </summary>
    public static class EntranceValidator {
        public readonly struct ValidationError {
            public readonly string Message;
            public readonly UnityEngine.Object Context;

            public ValidationError(string message, UnityEngine.Object context) {
                Message = message;
                Context = context;
            }
        }

        /// <summary>
        /// Checks that the given entranceId is unique within the scene, excluding the specified entrance.
        /// </summary>
        public static bool IsEntranceIdUniqueInScene(Scene scene, Entrance except, string entranceId) {
            var entrances = EntranceUtils.GetEntrancesInScene(scene);
            for (var i = 0; i < entrances.Count; i++) {
                var e = entrances[i];
                if (e == null || e == except) {
                    continue;
                }

                if (string.Equals(e.EntranceId, entranceId, StringComparison.Ordinal)) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Validates entrance ids (format + uniqueness) and links (scene exists + target entrance exists).
        /// </summary>
        public static List<ValidationError> ValidateScene(Scene scene) {
            var errors = new List<ValidationError>();

            var entrances = EntranceUtils.GetEntrancesInScene(scene);

            var map = new Dictionary<string, Entrance>(StringComparer.Ordinal);
            for (var i = 0; i < entrances.Count; i++) {
                var entrance = entrances[i];
                if (entrance == null) {
                    continue;
                }

                if (!IdUtils.IsValidId(entrance.EntranceId)) {
                    errors.Add(new ValidationError(
                        $"Entrance has invalid EntranceId '{entrance.EntranceId}'. Allowed [0-9a-zA-Z_-], length 1..64.",
                        entrance
                    ));
                    continue;
                }

                if (map.TryGetValue(entrance.EntranceId, out var other) && other != null) {
                    errors.Add(new ValidationError(
                        $"Duplicate EntranceId '{entrance.EntranceId}' in scene '{scene.path}'.", entrance));
                } else {
                    map[entrance.EntranceId] = entrance;
                }
            }

            var currentSceneGuid = DoorEditorUtils.GetSceneGuid(scene.path);

            for (var i = 0; i < entrances.Count; i++) {
                var entrance = entrances[i];
                if (entrance == null) {
                    continue;
                }

                var link = entrance.Link;

                if (link.TargetScene.IsEmpty()) {
                    errors.Add(new ValidationError($"Entrance '{entrance.EntranceId}' has no Target Scene.", entrance));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(link.TargetEntranceId)) {
                    errors.Add(new ValidationError(
                        $"Entrance '{entrance.EntranceId}' has empty Target Entrance ID.", entrance));
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(currentSceneGuid) &&
                    string.Equals(link.TargetScene.SceneGuid, currentSceneGuid, StringComparison.Ordinal) &&
                    string.Equals(link.TargetEntranceId, entrance.EntranceId, StringComparison.Ordinal)) {
                    errors.Add(new ValidationError(
                        $"Entrance '{entrance.EntranceId}' points to itself. Self-links are not allowed.", entrance));
                    continue;
                }

                var targetSceneGuid = link.TargetScene.SceneGuid;
                var targetScenePath = AssetDatabase.GUIDToAssetPath(targetSceneGuid);

                var cachedPath = link.TargetScene.ScenePath;
                if (!string.IsNullOrWhiteSpace(cachedPath) &&
                    !string.Equals(cachedPath, targetScenePath, StringComparison.Ordinal)) {
                    Debug.LogWarning(
                        $"Entrance '{entrance.EntranceId}' has outdated SceneReference cache. " +
                        $"Cached: '{cachedPath}', Actual: '{targetScenePath}'. " +
                        "Run: Tools/Doors/Repair Door Scene References",
                        entrance
                    );
                }

                if (string.IsNullOrWhiteSpace(targetScenePath)) {
                    errors.Add(new ValidationError(
                        $"Entrance '{entrance.EntranceId}' points to missing scene GUID '{targetSceneGuid}'.",
                        entrance));
                    continue;
                }

                if (!SceneContainsEntranceId(targetScenePath, link.TargetEntranceId)) {
                    errors.Add(new ValidationError(
                        $"Entrance '{entrance.EntranceId}' points to missing target entrance '{link.TargetEntranceId}' in scene '{targetScenePath}'.",
                        entrance
                    ));
                }
            }

            return errors;
        }

        private static bool SceneContainsEntranceId(string scenePath, string entranceId) {
            var result = false;
            DoorEditorUtils.ExecuteInScene(scenePath, scene => {
                result = EntranceUtils.FindEntranceByIdInScene(scene, entranceId) != null;
            });
            return result;
        }
    }
}
#endif
