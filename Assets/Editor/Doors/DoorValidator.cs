#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Doors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Editor.Doors {
    /// <summary>
    /// Editor-only validation for door ids and door links.
    /// Used by menu validation and build-time validation.
    /// </summary>
    public static class DoorValidator {
        public readonly struct ValidationError {
            public readonly string Message;
            public readonly UnityEngine.Object Context;

            public ValidationError(string message, UnityEngine.Object context) {
                Message = message;
                Context = context;
            }
        }


        /// <summary>
        /// Checks that the given doorId is unique within the scene, excluding the specified door.
        /// </summary>
        public static bool IsDoorIdUniqueInScene(Scene scene, Door except, string doorId) {
            var doors = DoorUtils.GetDoorsInScene(scene);
            for (var i = 0; i < doors.Count; i++) {
                var d = doors[i];
                if (d == null || d == except) {
                    continue;
                }

                if (string.Equals(d.DoorId, doorId, StringComparison.Ordinal)) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Validates door ids (format + uniqueness) and links (scene exists + target door exists).
        /// </summary>
        public static List<ValidationError> ValidateScene(Scene scene) {
            var errors = new List<ValidationError>();

            var doors = DoorUtils.GetDoorsInScene(scene);

            var map = new Dictionary<string, Door>(StringComparer.Ordinal);
            for (var i = 0; i < doors.Count; i++) {
                var door = doors[i];
                if (door == null) {
                    continue;
                }

                if (!DoorIdUtils.IsValidId(door.DoorId)) {
                    errors.Add(new ValidationError(
                        $"Door has invalid DoorId '{door.DoorId}'. Allowed [0-9a-zA-Z_-], length 1..64.",
                        door
                    ));
                    continue;
                }

                if (map.TryGetValue(door.DoorId, out var other) && other != null) {
                    errors.Add(new ValidationError($"Duplicate DoorId '{door.DoorId}' in scene '{scene.path}'.", door));
                } else {
                    map[door.DoorId] = door;
                }
            }

            var currentSceneGuid = DoorEditorUtils.GetSceneGuid(scene.path);

            for (var i = 0; i < doors.Count; i++) {
                var door = doors[i];
                if (door == null) {
                    continue;
                }

                var link = door.Link;

                if (link.TargetScene.IsEmpty()) {
                    errors.Add(new ValidationError($"Door '{door.DoorId}' has no Target Scene.", door));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(link.TargetDoorId)) {
                    errors.Add(new ValidationError($"Door '{door.DoorId}' has empty Target Door ID.", door));
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(currentSceneGuid) &&
                    string.Equals(link.TargetScene.SceneGuid, currentSceneGuid, StringComparison.Ordinal) &&
                    string.Equals(link.TargetDoorId, door.DoorId, StringComparison.Ordinal)) {
                    errors.Add(new ValidationError(
                        $"Door '{door.DoorId}' points to itself. Self-links are not allowed.", door));
                    continue;
                }

                var targetSceneGuid = link.TargetScene.SceneGuid;
                var targetScenePath = AssetDatabase.GUIDToAssetPath(targetSceneGuid);

                if (string.IsNullOrWhiteSpace(targetScenePath)) {
                    errors.Add(new ValidationError(
                        $"Door '{door.DoorId}' points to missing scene GUID '{targetSceneGuid}'.", door));
                    continue;
                }

                if (!SceneContainsDoorId(targetScenePath, link.TargetDoorId)) {
                    errors.Add(new ValidationError(
                        $"Door '{door.DoorId}' points to missing target door '{link.TargetDoorId}' in scene '{targetScenePath}'.",
                        door
                    ));
                }
            }

            return errors;
        }

        private static bool SceneContainsDoorId(string scenePath, string doorId) {
            var result = false;
            DoorEditorUtils.ExecuteInScene(scenePath, scene => {
                result = DoorUtils.FindDoorByIdInScene(scene, doorId) != null;
            });
            return result;
        }
    }
}
#endif
