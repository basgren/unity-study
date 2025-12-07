using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Tiles {
    /// <summary>
    /// A tile that repeats a rectangular sprite pattern across the tilemap,
    /// selecting the sprite based on tile coordinates modulo the pattern size.
    /// 
    /// Sprites are expected in the same order as Unity's Grid slicing:
    /// row-major, top row first, left to right, then the next row below, etc.
    /// This allows you to slice a texture with the Sprite Editor (Grid),
    /// select all generated sprites, and drag them directly into the array.
    /// </summary>
    [CreateAssetMenu(menuName = "Tiles/Pattern Grid Tile")]
    public class PatternGridTile : TileBase {
        /// <summary>
        /// Number of tiles horizontally in the repeating pattern.
        /// Must be at least 1.
        /// </summary>
        [Min(1)]
        [Tooltip("Number of tiles horizontally in the repeating pattern.")]
        public int patternWidth = 3;

        /// <summary>
        /// Number of tiles vertically in the repeating pattern.
        /// Must be at least 1.
        /// </summary>
        [Min(1)]
        [Tooltip("Number of tiles vertically in the repeating pattern.")]
        public int patternHeight = 3;

        /// <summary>
        /// Sprites in row-major order, top row first, left-to-right, then the next rows below.
        /// This matches Unity's default Grid slicing order in the Sprite Editor.
        /// Array length must be exactly patternWidth * patternHeight.
        /// </summary>
        [Tooltip("Sprites in row-major order, top row first, left-to-right. Matches Unity's Grid slicing order.")]
        public Sprite[] sprites;

        /// <summary>
        /// Optional X offset, in tile units, used to shift the pattern origin horizontally.
        /// This can be used to align the pattern to a specific point on the tilemap.
        /// </summary>
        [Tooltip("Optional X offset (in tile units) to shift the pattern origin horizontally.")]
        public int offsetX;

        /// <summary>
        /// Optional Y offset, in tile units, used to shift the pattern origin vertically.
        /// This can be used to align the pattern to a specific point on the tilemap.
        /// </summary>
        [Tooltip("Optional Y offset (in tile units) to shift the pattern origin vertically.")]
        public int offsetY;

        /// <summary>
        /// Collider type for this tile. Set to None for purely decorative backgrounds.
        /// </summary>
        [Tooltip("Collider type for this tile.")]
        public Tile.ColliderType colliderType = Tile.ColliderType.None;

        /// <inheritdoc />
        public override void GetTileData(
            Vector3Int position,
            ITilemap tilemap,
            ref TileData tileData
        ) {
            tileData.colliderType = colliderType;
            tileData.flags = TileFlags.LockTransform;
            tileData.transform = Matrix4x4.identity;

            if (!HasValidSpriteArray()) {
                tileData.sprite = null;
                return;
            }

            int x = Mod(position.x - offsetX, patternWidth);
            int y = Mod(position.y - offsetY, patternHeight);

            // Unity's sliced sprites: row 0 is the TOP row.
            // Our modulo Y grows downward, so we need to flip it.
            int unityRow = patternHeight - 1 - y;

            int index = x + unityRow * patternWidth;
            tileData.sprite = sprites[index];
        }

        /// <inheritdoc />
        public override void RefreshTile(Vector3Int position, ITilemap tilemap) {
            // Pattern depends only on coordinates, so refreshing the current tile is enough.
            tilemap.RefreshTile(position);
        }

        /// <summary>
        /// Checks whether the sprite array is correctly initialized
        /// and matches the expected size defined by patternWidth and patternHeight.
        /// </summary>
        private bool HasValidSpriteArray() {
            if (patternWidth < 1 || patternHeight < 1) {
                return false;
            }

            int expectedLength = patternWidth * patternHeight;
            return sprites != null && sprites.Length == expectedLength;
        }

        /// <summary>
        /// A modulo operation that always returns a non-negative result,
        /// even for negative input values.
        /// </summary>
        private static int Mod(int value, int modulo) {
            int r = value % modulo;
            if (r < 0) {
                r += modulo;
            }

            return r;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Ensures that pattern dimensions are valid and that the sprite array
        /// always has exactly patternWidth * patternHeight elements.
        /// Automatically resizes the array while preserving existing entries.
        /// </summary>
        private void OnValidate() {
            if (patternWidth < 1) {
                patternWidth = 1;
            }

            if (patternHeight < 1) {
                patternHeight = 1;
            }

            int targetLength = patternWidth * patternHeight;

            if (sprites == null || sprites.Length != targetLength) {
                Sprite[] newSprites = new Sprite[targetLength];

                if (sprites != null) {
                    int copyLength = Math.Min(sprites.Length, targetLength);
                    Array.Copy(sprites, newSprites, copyLength);
                }

                sprites = newSprites;
            }
        }
#endif
    }
}
