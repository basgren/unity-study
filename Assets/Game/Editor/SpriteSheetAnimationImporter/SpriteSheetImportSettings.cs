using System.Collections.Generic;
using UnityEngine;

namespace Game.Editor.SpriteSheetAnimationImporter {
    /// <summary>
    /// Row order interpretation for the source sprite sheet.
    /// Unity texture space is bottom-left-origin, but artists typically author rows top-down.
    /// </summary>
    public enum RowOrder {
        TopToBottom,
        BottomToTop
    }

    /// <summary>
    /// Convenience pivot modes exposed to the user.
    /// <see cref="CustomPixel"/> lets the user specify pivot in pixels relative to each cell's bottom-left.
    /// </summary>
    public enum PivotMode {
        Center,
        BottomCenter,
        CustomPixel
    }

    /// <summary>
    /// Plain data container for the importer window.
    /// Not serialized to disk on purpose - the tool is intended for quick one-off runs.
    /// </summary>
    public sealed class SpriteSheetImportSettings {
        // Source.
        public Texture2D sourceTexture;
        public string outputFolder = "";

        // Slicing. Cell size is derived from texture size, rows/cols, margin and spacing
        // (same approach as Unity's Sprite Editor "Grid By Cell Count").
        public int rows = 1;
        public int columns = 1;
        public int marginX = 0;
        public int marginY = 0;
        public int spacingX = 0;
        public int spacingY = 0;
        public RowOrder rowOrder = RowOrder.TopToBottom;
        public bool skipEmptyCells = true;

        // Naming.
        public List<string> stateNames = new List<string>();

        // Pivot.
        public PivotMode pivotMode = PivotMode.BottomCenter;
        public Vector2Int pivotPixels = new Vector2Int(16, 0);

        // Animation.
        public bool createClips = true;
        public float clipFps = 10f;
        public bool loopClips = true;

        // Animator controller.
        public bool createController = true;
        public string controllerName = "";

        // Draft transitions. One entry per row/state, parallel to stateNames.
        // Each entry is a comma-separated list of target state names.
        public List<string> transitionsTo = new List<string>();
    }
}
