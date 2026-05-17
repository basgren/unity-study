#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game.Core.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Features.Portals.Common.Editor {
    /// <summary>
    /// Validates id format + uniqueness and link integrity for any portal kind.
    /// Per-kind specifics (component type, finder) come from <see cref="PortalKind"/>.
    /// </summary>
    public static class PortalValidator {
        /// <summary>
        /// True if no other portal of <paramref name="kind"/> in <paramref name="scene"/> already uses <paramref name="id"/>.
        /// </summary>
        public static bool IsIdUniqueInScene(PortalKind kind, Scene scene, IPortal except, string id) {
            foreach (var p in kind.GetPortalsInScene(scene)) {
                if (p == null || ReferenceEquals(p, except)) {
                    continue;
                }

                if (string.Equals(p.Id, id, StringComparison.Ordinal)) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Validates every portal of the given kind in the scene: id format, id uniqueness,
        /// link target scene exists, link target id exists in that scene, no self-links.
        /// Logs a warning (not an error) about GameObject-name collisions.
        /// </summary>
        public static List<PortalValidationError> ValidateScene(PortalKind kind, Scene scene) {
            var errors = new List<PortalValidationError>();
            var portals = new List<IPortal>(kind.GetPortalsInScene(scene));

            var map = new Dictionary<string, IPortal>(StringComparer.Ordinal);
            for (var i = 0; i < portals.Count; i++) {
                var portal = portals[i];
                if (portal == null) {
                    continue;
                }

                var ctx = portal as UnityEngine.Object;

                if (!IdUtils.TryParsePortalId(portal.Id, out _)) {
                    errors.Add(new PortalValidationError(
                        $"{kind.KindName} has invalid id '{portal.Id}'. Must be a positive integer string.",
                        ctx));
                    continue;
                }

                if (map.TryGetValue(portal.Id, out var other) && other != null) {
                    errors.Add(new PortalValidationError(
                        $"Duplicate {kind.KindName} id '{portal.Id}' in scene '{scene.path}'.", ctx));
                } else {
                    map[portal.Id] = portal;
                }
            }

            // Advisory: warn about GameObject-name collisions among portals of this kind.
            var nameCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < portals.Count; i++) {
                var p = portals[i];
                if (p == null || p.gameObject == null) {
                    continue;
                }

                var n = p.gameObject.name ?? string.Empty;
                nameCounts.TryGetValue(n, out var c);
                nameCounts[n] = c + 1;
            }

            foreach (var kv in nameCounts) {
                if (kv.Value > 1) {
                    Debug.LogWarning(
                        $"[Portals] {kv.Value} {kind.KindName.ToLowerInvariant()}s share GameObject name '{kv.Key}' " +
                        $"in scene '{scene.path}'. Consider renaming for clarity in the link dropdown.");
                }
            }

            var currentSceneGuid = PortalEditorUtils.GetSceneGuid(scene.path);

            for (var i = 0; i < portals.Count; i++) {
                var portal = portals[i];
                if (portal == null) {
                    continue;
                }

                var ctx = portal as UnityEngine.Object;
                var targetScene = portal.TargetScene;
                var targetId = portal.TargetId;

                if (targetScene.IsEmpty()) {
                    errors.Add(new PortalValidationError(
                        $"{kind.KindName} '{portal.Id}' has no Target Scene.", ctx));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(targetId)) {
                    errors.Add(new PortalValidationError(
                        $"{kind.KindName} '{portal.Id}' has empty Target {kind.KindName} id.", ctx));
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(currentSceneGuid) &&
                    string.Equals(targetScene.SceneGuid, currentSceneGuid, StringComparison.Ordinal) &&
                    string.Equals(targetId, portal.Id, StringComparison.Ordinal)) {
                    errors.Add(new PortalValidationError(
                        $"{kind.KindName} '{portal.Id}' points to itself. Self-links are not allowed.", ctx));
                    continue;
                }

                var targetSceneGuid = targetScene.SceneGuid;
                var targetScenePath = AssetDatabase.GUIDToAssetPath(targetSceneGuid);

                var cachedPath = targetScene.ScenePath;
                if (!string.IsNullOrWhiteSpace(cachedPath) &&
                    !string.Equals(cachedPath, targetScenePath, StringComparison.Ordinal)) {
                    Debug.LogWarning(
                        $"{kind.KindName} '{portal.Id}' has outdated SceneReference cache. " +
                        $"Cached: '{cachedPath}', Actual: '{targetScenePath}'. " +
                        "Run: Tools/Portals/Repair Scene References", ctx);
                }

                if (string.IsNullOrWhiteSpace(targetScenePath)) {
                    errors.Add(new PortalValidationError(
                        $"{kind.KindName} '{portal.Id}' points to missing scene GUID '{targetSceneGuid}'.", ctx));
                    continue;
                }

                if (!SceneContainsId(kind, targetScenePath, targetId)) {
                    errors.Add(new PortalValidationError(
                        $"{kind.KindName} '{portal.Id}' points to missing target " +
                        $"{kind.KindName.ToLowerInvariant()} '{targetId}' in scene '{targetScenePath}'.",
                        ctx));
                }
            }

            return errors;
        }

        private static bool SceneContainsId(PortalKind kind, string scenePath, string id) {
            var result = false;
            PortalEditorUtils.ExecuteInScene(scenePath, scene => {
                result = kind.FindPortalByIdInScene(scene, id) != null;
            });
            return result;
        }
    }
}
#endif
