using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Game.Editor.SpriteSheetAnimationImporter {
    /// <summary>
    /// Configures the TextureImporter of the source sprite sheet and writes the spritesheet metadata
    /// (rects, names, pivots) for the desired grid. Uses the modern ISpriteEditorDataProvider API
    /// so it interoperates with textures that are already in Multiple mode and avoids duplicate
    /// internal sprite GUIDs.
    /// </summary>
    public static class SpriteSheetSlicer {
        /// <summary>
        /// Per-cell info used by downstream builders so they don't have to re-derive names.
        /// </summary>
        public readonly struct SlicedCell {
            public readonly int row;
            public readonly int column;
            public readonly string stateName;
            public readonly string spriteName;

            public SlicedCell(int row, int column, string stateName, string spriteName) {
                this.row = row;
                this.column = column;
                this.stateName = stateName;
                this.spriteName = spriteName;
            }
        }

        /// <summary>
        /// Derived cell size plus a remainder report so the UI/validator can surface alignment issues.
        /// </summary>
        public readonly struct CellSizeInfo {
            public readonly int width;
            public readonly int height;
            public readonly int remainderX;
            public readonly int remainderY;
            public readonly bool isValid;

            public CellSizeInfo(int width, int height, int remainderX, int remainderY, bool isValid) {
                this.width = width;
                this.height = height;
                this.remainderX = remainderX;
                this.remainderY = remainderY;
                this.isValid = isValid;
            }
        }

        /// <summary>
        /// Derives per-cell width/height from the texture size, rows/cols, margin and spacing.
        /// Mirrors Unity's "Grid By Cell Count" Sprite Editor mode: integer division, leftover pixels
        /// are reported back so the caller can warn the user.
        /// </summary>
        public static CellSizeInfo ComputeCellSize(SpriteSheetImportSettings s) {
            if (s == null || s.sourceTexture == null || s.rows <= 0 || s.columns <= 0) {
                return new CellSizeInfo(0, 0, 0, 0, false);
            }

            var availableW = s.sourceTexture.width - 2 * s.marginX - Mathf.Max(0, s.columns - 1) * s.spacingX;
            var availableH = s.sourceTexture.height - 2 * s.marginY - Mathf.Max(0, s.rows - 1) * s.spacingY;
            if (availableW <= 0 || availableH <= 0) {
                return new CellSizeInfo(0, 0, 0, 0, false);
            }

            var cellW = availableW / s.columns;
            var cellH = availableH / s.rows;
            if (cellW <= 0 || cellH <= 0) {
                return new CellSizeInfo(cellW, cellH, 0, 0, false);
            }

            return new CellSizeInfo(cellW, cellH, availableW % s.columns, availableH % s.rows, true);
        }

        /// <summary>
        /// Writes slicing metadata and reimports the texture. Returns the per-cell layout of kept cells
        /// (empty cells are excluded when <see cref="SpriteSheetImportSettings.skipEmptyCells"/> is true).
        /// </summary>
        public static List<SlicedCell> Slice(SpriteSheetImportSettings s) {
            var cellSize = ComputeCellSize(s);
            if (!cellSize.isValid) {
                throw new System.InvalidOperationException(
                    "Cannot slice: computed cell size is invalid. Check rows, columns, margin and spacing.");
            }

            var path = AssetDatabase.GetAssetPath(s.sourceTexture);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;

            // Modern data provider handles sprite GUIDs cleanly and works even when the texture is
            // already sliced into Multiple mode.
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();

            var allCells = BuildCells(s);
            var keptCells = new List<SlicedCell>(allCells.Count);
            var rects = new List<SpriteRect>(allCells.Count);

            Texture2D pixelProbe = null;
            if (s.skipEmptyCells) {
                pixelProbe = LoadReadableCopy(path);
                if (pixelProbe == null) {
                    Debug.LogWarning(
                        "[SpriteSheetImporter] Skip Empty Cells is enabled but the source texture could " +
                        "not be decoded for pixel sampling. Empty cells will NOT be skipped.");
                }
            }

            var skippedEmpty = 0;
            try {
                for (int i = 0; i < allCells.Count; i++) {
                    var cell = allCells[i];
                    var rect = ComputeRect(s, cellSize, cell.row, cell.column);

                    if (pixelProbe != null && IsRectEmpty(pixelProbe, rect)) {
                        skippedEmpty++;
                        continue;
                    }

                    rects.Add(new SpriteRect {
                        name = cell.spriteName,
                        // Explicit unique GUID avoids "key already added" errors from duplicate default GUIDs.
                        spriteID = new GUID(System.Guid.NewGuid().ToString("N")),
                        rect = rect,
                        alignment = PivotModeToAlignment(s.pivotMode),
                        pivot = ComputePivotNormalized(s, cellSize),
                        border = Vector4.zero
                    });

                    keptCells.Add(cell);
                }
            }
            finally {
                if (pixelProbe != null) {
                    Object.DestroyImmediate(pixelProbe);
                }
            }

            if (s.skipEmptyCells) {
                Debug.Log(
                    $"[SpriteSheetImporter] Sliced {keptCells.Count} of {allCells.Count} cells " +
                    $"({skippedEmpty} empty skipped).");
            }

            dataProvider.SetSpriteRects(rects.ToArray());

            // Some Unity versions need the name<->fileId registry updated so the generated sub-assets
            // keep stable IDs across reimports. Safe no-op on versions that don't need it.
            var nameFileIdProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameFileIdProvider != null) {
                var pairs = new List<SpriteNameFileIdPair>(rects.Count);
                for (int i = 0; i < rects.Count; i++) {
                    pairs.Add(new SpriteNameFileIdPair(rects[i].name, rects[i].spriteID));
                }

                nameFileIdProvider.SetNameFileIdPairs(pairs);
            }

            dataProvider.Apply();
            importer.SaveAndReimport();

            return keptCells;
        }

        /// <summary>
        /// Builds the flat list of cells in row-major order (row 0 first, left-to-right).
        /// The visual row order (top-down vs. bottom-up) only affects rect Y, not this ordering.
        /// </summary>
        private static List<SlicedCell> BuildCells(SpriteSheetImportSettings s) {
            var cells = new List<SlicedCell>(s.rows * s.columns);
            for (int r = 0; r < s.rows; r++) {
                var stateName = s.stateNames[r];
                for (int c = 0; c < s.columns; c++) {
                    var spriteName = $"{stateName}_{c:00}";
                    cells.Add(new SlicedCell(r, c, stateName, spriteName));
                }
            }

            return cells;
        }

        /// <summary>
        /// Computes the sprite rect in texture space (Unity uses bottom-left origin).
        /// </summary>
        private static Rect ComputeRect(SpriteSheetImportSettings s, CellSizeInfo cell, int row, int column) {
            var texH = s.sourceTexture.height;

            var x = s.marginX + column * (cell.width + s.spacingX);

            int y;
            if (s.rowOrder == RowOrder.TopToBottom) {
                // Row 0 is the top row in the image.
                y = texH - s.marginY - (row + 1) * cell.height - row * s.spacingY;
            } else {
                // Row 0 is the bottom row in the image.
                y = s.marginY + row * (cell.height + s.spacingY);
            }

            return new Rect(x, y, cell.width, cell.height);
        }

        private static SpriteAlignment PivotModeToAlignment(PivotMode mode) {
            switch (mode) {
                case PivotMode.Center: return SpriteAlignment.Center;
                case PivotMode.BottomCenter: return SpriteAlignment.BottomCenter;
                case PivotMode.CustomPixel: return SpriteAlignment.Custom;
                default: return SpriteAlignment.Center;
            }
        }

        /// <summary>
        /// Normalized pivot for Unity. Only used when alignment == Custom; for other presets Unity
        /// derives the pivot from the alignment and the value passed here is ignored.
        /// </summary>
        private static Vector2 ComputePivotNormalized(SpriteSheetImportSettings s, CellSizeInfo cell) {
            if (s.pivotMode != PivotMode.CustomPixel) {
                return new Vector2(0.5f, 0.5f);
            }

            var nx = s.pivotPixels.x / (float)Mathf.Max(1, cell.width);
            var ny = s.pivotPixels.y / (float)Mathf.Max(1, cell.height);
            return new Vector2(Mathf.Clamp01(nx), Mathf.Clamp01(ny));
        }

        /// <summary>
        /// Loads the raw image file as a readable Texture2D (independent of the asset's import settings).
        /// Used only to sample alpha for empty-cell detection; caller disposes it.
        /// </summary>
        private static Texture2D LoadReadableCopy(string assetPath) {
            if (string.IsNullOrEmpty(assetPath)) {
                return null;
            }

            // Resolve against the project root. AssetDatabase returns paths like "Assets/.../foo.png",
            // which may not resolve via File.Exists if the process CWD is not the project root.
            string fullPath;
            try {
                fullPath = Path.GetFullPath(assetPath);
            }
            catch (System.Exception e) {
                Debug.LogWarning($"[SpriteSheetImporter] Cannot resolve path '{assetPath}': {e.Message}");
                return null;
            }

            if (!File.Exists(fullPath)) {
                Debug.LogWarning($"[SpriteSheetImporter] Texture file not found on disk: {fullPath}");
                return null;
            }

            byte[] bytes;
            try {
                bytes = File.ReadAllBytes(fullPath);
            }
            catch (System.Exception e) {
                Debug.LogWarning($"[SpriteSheetImporter] Failed to read '{fullPath}': {e.Message}");
                return null;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes)) {
                Debug.LogWarning($"[SpriteSheetImporter] LoadImage failed for '{fullPath}'. " +
                                 "Only PNG/JPG formats are supported for empty-cell detection.");
                Object.DestroyImmediate(tex);
                return null;
            }

            return tex;
        }

        /// <summary>
        /// Treats a cell as empty if every pixel inside its rect has alpha == 0.
        /// </summary>
        private static bool IsRectEmpty(Texture2D tex, Rect rect) {
            var x = Mathf.RoundToInt(rect.x);
            var y = Mathf.RoundToInt(rect.y);
            var w = Mathf.RoundToInt(rect.width);
            var h = Mathf.RoundToInt(rect.height);

            if (w <= 0 || h <= 0) {
                return true;
            }

            if (x < 0 || y < 0 || x + w > tex.width || y + h > tex.height) {
                return false;
            }

            // GetPixels has a region overload; GetPixels32 does not.
            var pixels = tex.GetPixels(x, y, w, h);
            for (int i = 0; i < pixels.Length; i++) {
                if (pixels[i].a != 0f) {
                    return false;
                }
            }

            return true;
        }
    }
}
