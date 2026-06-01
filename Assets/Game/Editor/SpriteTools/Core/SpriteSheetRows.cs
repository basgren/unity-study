using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.SpriteTools.Core {
    /// <summary>One sprite within a sheet: its current name and texture-space rect (bottom-left origin).</summary>
    public readonly struct SpriteCell {
        public readonly string Name;
        public readonly Rect Rect;

        public SpriteCell(string name, Rect rect) {
            Name = name;
            Rect = rect;
        }
    }

    /// <summary>
    /// Groups a sprite sheet's cells into rows. <see cref="Cluster"/> is the pure, testable core;
    /// <see cref="Detect"/> wraps it with asset loading. Only meaningful for grid-style sheets.
    /// </summary>
    public static class SpriteSheetRows {
        /// <summary>Loads the Sprite sub-assets of <paramref name="texturePath"/> and clusters them into rows.</summary>
        public static List<List<SpriteCell>> Detect(string texturePath) {
            var cells = new List<SpriteCell>();

            var assets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
            if (assets != null) {
                for (int i = 0; i < assets.Length; i++) {
                    var sprite = assets[i] as Sprite;
                    if (sprite != null) {
                        cells.Add(new SpriteCell(sprite.name, sprite.rect));
                    }
                }
            }

            return Cluster(cells);
        }

        /// <summary>
        /// Clusters cells into rows by their rect Y (top-to-bottom), ordered left-to-right within each
        /// row. Mirrors SpriteSheetSlicer's row-major layout. Pure: no asset access, so it is unit testable.
        /// </summary>
        public static List<List<SpriteCell>> Cluster(IReadOnlyList<SpriteCell> cells) {
            var rows = new List<List<SpriteCell>>();
            if (cells == null || cells.Count == 0) {
                return rows;
            }

            var ordered = new List<SpriteCell>(cells);
            // Texture space is bottom-left origin, so higher Y = higher up = earlier visual row.
            ordered.Sort((a, b) => {
                var cy = b.Rect.y.CompareTo(a.Rect.y);
                return cy != 0 ? cy : a.Rect.x.CompareTo(b.Rect.x);
            });

            List<SpriteCell> current = null;
            var rowY = 0f;
            for (int i = 0; i < ordered.Count; i++) {
                var cell = ordered[i];
                // Half a cell height cleanly separates adjacent grid rows while tolerating sub-pixel drift.
                var tolerance = Mathf.Max(1f, cell.Rect.height * 0.5f);
                if (current == null || Mathf.Abs(cell.Rect.y - rowY) > tolerance) {
                    current = new List<SpriteCell>();
                    rows.Add(current);
                    rowY = cell.Rect.y;
                }

                current.Add(cell);
            }

            for (int r = 0; r < rows.Count; r++) {
                rows[r].Sort((a, b) => a.Rect.x.CompareTo(b.Rect.x));
            }

            return rows;
        }

        /// <summary>Suggested name for a row: its first sprite's shared prefix, or "row" as a fallback.</summary>
        public static string DeriveRowName(IReadOnlyList<SpriteCell> row) {
            if (row == null || row.Count == 0) {
                return "row";
            }

            var name = SpriteNaming.StripNumericSuffix(row[0].Name);
            return string.IsNullOrEmpty(name) ? "row" : name;
        }
    }
}
