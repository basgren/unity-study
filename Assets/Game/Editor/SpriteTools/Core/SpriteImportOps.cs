using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Game.Editor.SpriteTools.Core {
    /// <summary>A pivot to assign to a sprite: an alignment preset plus a custom value used when the preset is Custom.</summary>
    public readonly struct PivotSetting {
        public readonly SpriteAlignment Alignment;

        /// <summary>Custom pivot value, interpreted as pixels or normalized per the <c>pixels</c> flag at apply time.</summary>
        public readonly Vector2 CustomPivot;

        public PivotSetting(SpriteAlignment alignment, Vector2 customPivot) {
            Alignment = alignment;
            CustomPivot = customPivot;
        }
    }

    /// <summary>
    /// UI-agnostic sprite import edits: pivot assignment and renaming. Each method operates on one asset
    /// path, performs the edit, reimports, and returns whether anything changed — no Selection, no GUI.
    /// </summary>
    /// <remarks>
    /// Multiple-mode edits go through <see cref="ISpriteEditorDataProvider"/>: Unity 2021+ ignores writes
    /// made via the obsolete <c>TextureImporter.spritesheet</c> setter for data-provider-managed sheets.
    /// Renames preserve each sprite's <c>spriteID</c> (and thus its sub-asset fileID), so existing
    /// references survive. Callers may wrap several calls in <c>AssetDatabase.StartAssetEditing</c>/
    /// <c>StopAssetEditing</c> to batch the reimports.
    /// </remarks>
    public static class SpriteImportOps {
        /// <summary>
        /// Assigns one pivot to a Single sprite or to the sprites of a Multiple sheet. For Multiple sheets a
        /// non-empty <paramref name="nameFilter"/> restricts the change to those sprite names; null/empty
        /// means every sprite.
        /// </summary>
        public static bool ApplyUniformPivot(string assetPath, SpriteAlignment alignment, Vector2 customPivot,
            bool pixels, ICollection<string> nameFilter) {
            var importer = GetSpriteImporter(assetPath);
            if (importer == null) {
                return false;
            }

            if (importer.spriteImportMode == SpriteImportMode.Single) {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (tex == null || tex.width <= 0 || tex.height <= 0) {
                    return false;
                }

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)alignment;
                if (alignment == SpriteAlignment.Custom) {
                    settings.spritePivot = ToNormalized(customPivot, pixels, new Rect(0f, 0f, tex.width, tex.height));
                }

                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                return true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Multiple) {
                return false;
            }

            var dataProvider = GetDataProvider(importer);
            if (dataProvider == null) {
                return false;
            }

            dataProvider.InitSpriteEditorDataProvider();
            var rects = dataProvider.GetSpriteRects();
            if (rects == null || rects.Length == 0) {
                return false;
            }

            var filter = nameFilter != null && nameFilter.Count > 0;
            var changed = false;
            for (int i = 0; i < rects.Length; i++) {
                if (filter && !nameFilter.Contains(rects[i].name)) {
                    continue;
                }

                rects[i].alignment = alignment;
                if (alignment == SpriteAlignment.Custom) {
                    rects[i].pivot = ToNormalized(customPivot, pixels, rects[i].rect);
                }

                changed = true;
            }

            if (!changed) {
                return false;
            }

            dataProvider.SetSpriteRects(rects);
            dataProvider.Apply();
            importer.SaveAndReimport();
            return true;
        }

        /// <summary>Assigns a per-sprite pivot to the sprites of a Multiple sheet, keyed by sprite name.</summary>
        public static bool ApplyPivots(string assetPath, IReadOnlyDictionary<string, PivotSetting> pivotByName,
            bool pixels) {
            if (pivotByName == null || pivotByName.Count == 0) {
                return false;
            }

            var importer = GetSpriteImporter(assetPath);
            if (importer == null || importer.spriteImportMode != SpriteImportMode.Multiple) {
                return false;
            }

            var dataProvider = GetDataProvider(importer);
            if (dataProvider == null) {
                return false;
            }

            dataProvider.InitSpriteEditorDataProvider();
            var rects = dataProvider.GetSpriteRects();
            if (rects == null || rects.Length == 0) {
                return false;
            }

            var changed = false;
            for (int i = 0; i < rects.Length; i++) {
                if (!pivotByName.TryGetValue(rects[i].name, out var setting)) {
                    continue;
                }

                rects[i].alignment = setting.Alignment;
                if (setting.Alignment == SpriteAlignment.Custom) {
                    rects[i].pivot = ToNormalized(setting.CustomPivot, pixels, rects[i].rect);
                }

                changed = true;
            }

            if (!changed) {
                return false;
            }

            dataProvider.SetSpriteRects(rects);
            dataProvider.Apply();
            importer.SaveAndReimport();
            return true;
        }

        /// <summary>
        /// Renames sprites of a Multiple sheet (map of current name → new name). Preserves each sprite's
        /// spriteID and re-registers the name↔fileId map, so the sub-asset fileID — and references to it —
        /// survive the rename.
        /// </summary>
        public static bool Rename(string assetPath, IReadOnlyDictionary<string, string> oldToNew) {
            if (oldToNew == null || oldToNew.Count == 0) {
                return false;
            }

            var importer = GetSpriteImporter(assetPath);
            if (importer == null) {
                return false;
            }

            // Only Multiple-mode sheets have nameable sub-sprites; a Single sprite's name tracks the asset.
            if (importer.spriteImportMode != SpriteImportMode.Multiple) {
                Debug.LogWarning($"Rename skipped (not a Multiple-mode sprite sheet): {assetPath}");
                return false;
            }

            var dataProvider = GetDataProvider(importer);
            if (dataProvider == null) {
                return false;
            }

            dataProvider.InitSpriteEditorDataProvider();
            var rects = dataProvider.GetSpriteRects();
            if (rects == null || rects.Length == 0) {
                return false;
            }

            var changed = false;
            for (int i = 0; i < rects.Length; i++) {
                if (oldToNew.TryGetValue(rects[i].name, out var newName) && rects[i].name != newName) {
                    // Only the display name changes; spriteID is left intact so the fileID survives.
                    rects[i].name = newName;
                    changed = true;
                }
            }

            if (!changed) {
                return false;
            }

            dataProvider.SetSpriteRects(rects);

            // Re-register every sprite's (name, spriteID) so the stable name↔fileId map tracks the new names.
            var nameFileIdProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameFileIdProvider != null) {
                var pairs = new List<SpriteNameFileIdPair>(rects.Length);
                for (int i = 0; i < rects.Length; i++) {
                    pairs.Add(new SpriteNameFileIdPair(rects[i].name, rects[i].spriteID));
                }

                nameFileIdProvider.SetNameFileIdPairs(pairs);
            }

            dataProvider.Apply();
            importer.SaveAndReimport();
            return true;
        }

        private static TextureImporter GetSpriteImporter(string assetPath) {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null || importer.textureType != TextureImporterType.Sprite) {
                return null;
            }

            return importer;
        }

        private static ISpriteEditorDataProvider GetDataProvider(TextureImporter importer) {
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            return factory.GetSpriteEditorDataProviderFromObject(importer);
        }

        // Converts a custom pivot (pixels or already-normalized) to normalized for one rect.
        private static Vector2 ToNormalized(Vector2 value, bool pixels, Rect rect) {
            if (!pixels) {
                return Clamp01(value);
            }

            var nx = value.x / Mathf.Max(1f, rect.width);
            var ny = value.y / Mathf.Max(1f, rect.height);
            return Clamp01(new Vector2(nx, ny));
        }

        private static Vector2 Clamp01(Vector2 v) {
            v.x = Mathf.Clamp01(v.x);
            v.y = Mathf.Clamp01(v.y);
            return v;
        }
    }
}
