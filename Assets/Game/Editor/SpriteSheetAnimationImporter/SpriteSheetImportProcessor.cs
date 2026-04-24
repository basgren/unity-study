using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.SpriteSheetAnimationImporter {
    /// <summary>
    /// Summary of a dry-run or a completed import, used by the window to display compact stats.
    /// </summary>
    public sealed class ImportSummary {
        public int spriteCount;
        public int clipCount;
        public bool willCreateController;
        public int draftTransitionCount;
        public readonly List<string> unknownTransitionTargets = new List<string>();
    }

    /// <summary>
    /// Orchestrates the sprite sheet import: slicing, clip generation, controller generation.
    /// Keeps dialog/UI code out of the individual builders.
    /// </summary>
    public static class SpriteSheetImportProcessor {
        /// <summary>
        /// Resolves the output folder to use for generated clips and controller.
        /// If the user set an explicit folder, use it. Otherwise default to a per-sheet subfolder
        /// next to the texture, named after the controller (falling back to the texture name).
        /// This keeps each sheet's generated assets isolated so multiple imports don't collide.
        /// </summary>
        public static string ResolveOutputFolder(SpriteSheetImportSettings s) {
            if (!string.IsNullOrWhiteSpace(s.outputFolder)) {
                return s.outputFolder.TrimEnd('/', '\\');
            }

            var texPath = AssetDatabase.GetAssetPath(s.sourceTexture);
            var dir = Path.GetDirectoryName(texPath);
            var baseFolder = string.IsNullOrEmpty(dir) ? "Assets" : dir.Replace('\\', '/');

            var subfolder = !string.IsNullOrWhiteSpace(s.controllerName)
                ? s.controllerName
                : (s.sourceTexture != null ? s.sourceTexture.name : "");

            return string.IsNullOrWhiteSpace(subfolder) ? baseFolder : $"{baseFolder}/{subfolder}";
        }

        /// <summary>
        /// Cheap summary without touching any asset - used to populate the preview panel and
        /// the overwrite confirmation dialog.
        /// </summary>
        public static ImportSummary BuildPreviewSummary(SpriteSheetImportSettings s) {
            var summary = new ImportSummary {
                spriteCount = s.rows * s.columns,
                clipCount = s.createClips ? s.rows : 0,
                willCreateController = s.createController,
                draftTransitionCount = CountPlannedTransitions(s)
            };
            return summary;
        }

        /// <summary>
        /// Lists the asset paths that would be overwritten by a run with these settings.
        /// Used to build a clear confirmation message.
        /// </summary>
        public static List<string> CollectOverwriteTargets(SpriteSheetImportSettings s, string outputFolder) {
            var targets = new List<string>();

            var texPath = AssetDatabase.GetAssetPath(s.sourceTexture);
            var importer = texPath != null ? AssetImporter.GetAtPath(texPath) as TextureImporter : null;
            if (importer != null && importer.spriteImportMode == SpriteImportMode.Multiple) {
                targets.Add($"Sprite slicing metadata: {texPath}");
            }

            if (s.createClips) {
                for (int i = 0; i < s.stateNames.Count; i++) {
                    var path = $"{outputFolder}/{s.stateNames[i]}.anim";
                    if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) != null) {
                        targets.Add(path);
                    }
                }
            }

            if (s.createController) {
                var controllerPath = $"{outputFolder}/{s.controllerName}.controller";
                if (AssetDatabase.LoadAssetAtPath<Object>(controllerPath) != null) {
                    targets.Add(controllerPath);
                }
            }

            return targets;
        }

        /// <summary>
        /// Runs the full pipeline. Caller is responsible for validation and overwrite confirmation.
        /// </summary>
        public static ImportSummary Process(SpriteSheetImportSettings s) {
            var summary = new ImportSummary();
            var outputFolder = ResolveOutputFolder(s);

            EnsureFolderExists(outputFolder);

            // Slicer runs outside StartAssetEditing because the clip builder below needs the
            // reimported sprite sub-assets to exist before it can reference them by name.
            var cells = SpriteSheetSlicer.Slice(s);
            summary.spriteCount = cells.Count;

            Dictionary<string, AnimationClip> clipsByState = null;
            if (s.createClips) {
                clipsByState = SpriteSheetAnimationClipBuilder.BuildClips(s, outputFolder);
                summary.clipCount = clipsByState.Count;
            }

            if (s.createController && clipsByState != null && clipsByState.Count > 0) {
                var report = SpriteSheetAnimatorControllerBuilder.BuildOrUpdate(s, outputFolder, clipsByState);
                summary.willCreateController = report.controller != null;
                summary.draftTransitionCount = report.createdTransitions;
                summary.unknownTransitionTargets.AddRange(report.unknownTargets);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return summary;
        }

        private static int CountPlannedTransitions(SpriteSheetImportSettings s) {
            if (!s.createController || s.transitionsTo == null) {
                return 0;
            }

            // Build a set of state names that would exist.
            var existing = new HashSet<string>();
            for (int i = 0; i < s.stateNames.Count; i++) {
                if (!string.IsNullOrWhiteSpace(s.stateNames[i])) {
                    existing.Add(s.stateNames[i]);
                }
            }

            var count = 0;
            for (int row = 0; row < s.stateNames.Count && row < s.transitionsTo.Count; row++) {
                var raw = s.transitionsTo[row];
                if (string.IsNullOrWhiteSpace(raw)) {
                    continue;
                }

                var parts = raw.Split(',');
                for (int i = 0; i < parts.Length; i++) {
                    var trimmed = parts[i].Trim();
                    if (trimmed.Length == 0) {
                        continue;
                    }

                    if (existing.Contains(trimmed)) {
                        count++;
                    }
                }
            }

            return count;
        }

        private static void EnsureFolderExists(string assetFolder) {
            if (AssetDatabase.IsValidFolder(assetFolder)) {
                return;
            }

            // Walk the path and create each missing folder.
            var parts = assetFolder.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets") {
                return;
            }

            var current = "Assets";
            for (int i = 1; i < parts.Length; i++) {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
