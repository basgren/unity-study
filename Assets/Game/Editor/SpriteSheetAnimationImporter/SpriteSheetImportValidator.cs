using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.SpriteSheetAnimationImporter {
    /// <summary>
    /// Collected validation output. Errors block processing; warnings are informational.
    /// </summary>
    public sealed class ValidationReport {
        public readonly List<string> errors = new List<string>();
        public readonly List<string> warnings = new List<string>();

        public bool HasErrors => errors.Count > 0;
        public bool HasWarnings => warnings.Count > 0;

        public void Error(string msg) {
            errors.Add(msg);
        }

        public void Warn(string msg) {
            warnings.Add(msg);
        }
    }

    /// <summary>
    /// Validates user input prior to processing.
    /// </summary>
    public static class SpriteSheetImportValidator {
        public static ValidationReport Validate(SpriteSheetImportSettings s) {
            var report = new ValidationReport();

            if (s == null) {
                report.Error("Settings are null.");
                return report;
            }

            if (s.sourceTexture == null) {
                report.Error("Source texture is not assigned.");
            }

            if (s.rows <= 0) {
                report.Error("Rows must be greater than 0.");
            }

            if (s.columns <= 0) {
                report.Error("Columns must be greater than 0.");
            }

            if (s.marginX < 0 || s.marginY < 0) {
                report.Error("Margin must be non-negative.");
            }

            if (s.spacingX < 0 || s.spacingY < 0) {
                report.Error("Spacing must be non-negative.");
            }

            if (s.clipFps <= 0f) {
                report.Error("Clip FPS must be greater than 0.");
            }

            // Cell size is derived from texture size, rows/cols, margin and spacing.
            if (s.sourceTexture != null && s.rows > 0 && s.columns > 0) {
                var path = AssetDatabase.GetAssetPath(s.sourceTexture);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) {
                    report.Error("Source texture has no TextureImporter (is it a Texture2D asset?).");
                } else {
                    var cell = SpriteSheetSlicer.ComputeCellSize(s);
                    if (!cell.isValid) {
                        report.Error(
                            $"Cannot derive a positive cell size from texture " +
                            $"{s.sourceTexture.width}x{s.sourceTexture.height} with current margin/spacing.");
                    } else {
                        if (cell.remainderX != 0 || cell.remainderY != 0) {
                            report.Warn(
                                $"Texture does not divide evenly. Leftover pixels: " +
                                $"X={cell.remainderX}, Y={cell.remainderY}. " +
                                $"Cells will be {cell.width}x{cell.height} (rounded down).");
                        }
                    }
                }
            }

            // Names list must cover each row.
            if (s.rows > 0) {
                if (s.stateNames == null || s.stateNames.Count != s.rows) {
                    report.Error(
                        $"State name count ({(s.stateNames == null ? 0 : s.stateNames.Count)}) " +
                        $"does not match rows ({s.rows}). Fix the list before processing.");
                } else {
                    var seen = new HashSet<string>();
                    for (int i = 0; i < s.stateNames.Count; i++) {
                        var name = s.stateNames[i];
                        if (string.IsNullOrWhiteSpace(name)) {
                            report.Error($"State name for row {i} is empty.");
                            continue;
                        }

                        if (!IsSafeAssetName(name)) {
                            report.Error($"State name '{name}' at row {i} contains invalid characters.");
                            continue;
                        }

                        if (!seen.Add(name)) {
                            report.Error($"State name '{name}' is duplicated.");
                        }
                    }
                }
            }

            // Pivot sanity - compare against the computed cell size when possible.
            if (s.pivotMode == PivotMode.CustomPixel && s.sourceTexture != null) {
                var cell = SpriteSheetSlicer.ComputeCellSize(s);
                if (cell.isValid) {
                    if (s.pivotPixels.x < 0 || s.pivotPixels.x > cell.width ||
                        s.pivotPixels.y < 0 || s.pivotPixels.y > cell.height) {
                        report.Warn(
                            $"Custom pivot ({s.pivotPixels.x}, {s.pivotPixels.y}) is outside the cell " +
                            $"({cell.width}x{cell.height}). Unity will clamp to 0..1.");
                    }
                }
            }

            // Controller name.
            if (s.createController) {
                if (string.IsNullOrWhiteSpace(s.controllerName)) {
                    report.Error("Controller name is empty.");
                } else if (!IsSafeAssetName(s.controllerName)) {
                    report.Error($"Controller name '{s.controllerName}' contains invalid characters.");
                }
            }

            return report;
        }

        /// <summary>
        /// Rejects characters that are unsafe in asset file names on Windows/macOS.
        /// </summary>
        private static bool IsSafeAssetName(string s) {
            if (string.IsNullOrEmpty(s)) {
                return false;
            }

            for (int i = 0; i < s.Length; i++) {
                var c = s[i];
                if (c == '/' || c == '\\' || c == ':' || c == '*' || c == '?' ||
                    c == '"' || c == '<' || c == '>' || c == '|') {
                    return false;
                }
            }

            return true;
        }
    }
}
