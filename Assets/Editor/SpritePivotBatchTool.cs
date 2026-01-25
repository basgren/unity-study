// SpritePivotBatchTool.cs
// Place into: Assets/Editor/

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EditorTools.Sprites {
    /// <summary>
    /// Batch tool for assigning Sprite pivot import settings in a Sprite Editor–style workflow:
    /// <list type="bullet">
    /// <item><description>Select a Pivot preset (all <see cref="SpriteAlignment"/> options + Custom).</description></item>
    /// <item><description>Select Pivot Unit Mode (Normalized / Pixels).</description></item>
    /// <item><description>When Pivot is Custom, enter a custom pivot value.</description></item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unity stores sprite pivots as normalized coordinates (0..1). "Pixels" mode is a UI convenience, so this tool
    /// converts pixels to normalized per sprite rect:
    /// <c>(px / rect.width, py / rect.height)</c>.
    /// </para>
    /// <para>
    /// Pivot coordinates are interpreted from the bottom-left of each sprite rect, matching Sprite Editor behavior.
    /// </para>
    /// <para>
    /// Unity 2019: alignment and pivot for Single sprites are set via <see cref="TextureImporterSettings"/>
    /// using <see cref="TextureImporter.ReadTextureSettings"/> and <see cref="TextureImporter.SetTextureSettings"/>.
    /// </para>
    /// <para>
    /// Works with Sprite textures (<see cref="TextureImporterType.Sprite"/>) in <c>Single</c> and <c>Multiple</c> modes.
    /// For spritesheets (Multiple) it can apply to all sprites in the sheet or only to selected Sprite sub-assets.
    /// </para>
    /// <para>
    /// Note: Import setting changes may not be reliably undoable. Prefer version control for safety.
    /// </para>
    /// </remarks>
    public sealed class SpritePivotBatchTool : EditorWindow {
        private enum PivotUnitMode {
            Normalized,
            Pixels
        }

        [SerializeField]
        private SpriteAlignment pivotPreset = SpriteAlignment.Center;

        [SerializeField]
        private PivotUnitMode unitMode = PivotUnitMode.Pixels;

        [SerializeField]
        private Vector2 customPivot = new Vector2(16f, 0f);

        [SerializeField]
        private bool applyToAllSpritesInSheet = true;

        [MenuItem("Tools/Sprites/Batch Pivot (Sprite Editor Style)")]
        public static void Open() {
            var wnd = GetWindow<SpritePivotBatchTool>("Batch Sprite Pivot");
            wnd.minSize = new Vector2(420f, 240f);
            wnd.Show();
        }

        private void OnGUI() {
            EditorGUILayout.LabelField("Pivot", EditorStyles.boldLabel);
            pivotPreset = (SpriteAlignment)EditorGUILayout.EnumPopup("Pivot Preset", pivotPreset);

            using (new EditorGUI.DisabledScope(pivotPreset != SpriteAlignment.Custom)) {
                unitMode = (PivotUnitMode)EditorGUILayout.EnumPopup("Pivot Unit Mode", unitMode);

                var label = unitMode == PivotUnitMode.Pixels ? "Custom Pivot (px)" : "Custom Pivot (0..1)";
                customPivot = EditorGUILayout.Vector2Field(label, customPivot);
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Spritesheet (Multiple)", EditorStyles.boldLabel);
            applyToAllSpritesInSheet =
                EditorGUILayout.ToggleLeft("Apply to all sprites in sheet", applyToAllSpritesInSheet);

            EditorGUILayout.Space();

            if (GUILayout.Button("Apply To Selection")) {
                ApplyToSelection();
            }

            EditorGUILayout.HelpBox(
                "Select Texture2D sprite textures and/or Sprite sub-assets in the Project window.\n" +
                "Custom pivot is interpreted from the bottom-left of each sprite rect, like Sprite Editor.",
                MessageType.Info
            );
        }

        private void ApplyToSelection() {
            var targets = CollectSelection();
            if (targets.Count == 0) {
                Debug.LogWarning("No valid Sprite textures/sprites selected.");
                return;
            }

            var changed = 0;
            var skipped = 0;

            AssetDatabase.StartAssetEditing();
            try {
                foreach (var kv in targets) {
                    var assetPath = kv.Key;
                    var selectedSpriteNames = kv.Value;

                    var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    if (importer == null) {
                        skipped++;
                        continue;
                    }

                    if (importer.textureType != TextureImporterType.Sprite) {
                        skipped++;
                        continue;
                    }

                    var didChange = ApplyToImporter(importer, selectedSpriteNames);
                    if (didChange) {
                        changed++;
                        importer.SaveAndReimport();
                    } else {
                        skipped++;
                    }
                }
            }
            finally {
                AssetDatabase.StopAssetEditing();
            }

            Debug.Log($"Batch pivot finished. Changed: {changed}, skipped: {skipped}");
        }

        private static Dictionary<string, HashSet<string>> CollectSelection() {
            var result = new Dictionary<string, HashSet<string>>();

            var objects = Selection.objects;
            if (objects == null || objects.Length == 0) {
                return result;
            }

            for (int i = 0; i < objects.Length; i++) {
                var obj = objects[i];
                if (obj == null) {
                    continue;
                }

                var path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) {
                    continue;
                }

                var sprite = obj as Sprite;
                if (sprite != null) {
                    if (!result.TryGetValue(path, out var set) || set == null) {
                        set = new HashSet<string>();
                        result[path] = set;
                    }

                    set.Add(sprite.name);
                    continue;
                }

                var tex = obj as Texture2D;
                if (tex != null) {
                    if (!result.ContainsKey(path)) {
                        result[path] = null;
                    }
                }
            }

            return result;
        }

        private bool ApplyToImporter(TextureImporter importer, HashSet<string> selectedSpriteNames) {
            if (importer.spriteImportMode == SpriteImportMode.Single) {
                return ApplySingle(importer);
            }

            if (importer.spriteImportMode == SpriteImportMode.Multiple) {
                return ApplyMultiple(importer, selectedSpriteNames);
            }

            return false;
        }

        private bool ApplySingle(TextureImporter importer) {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(importer.assetPath);
            if (tex == null || tex.width <= 0 || tex.height <= 0) {
                return false;
            }

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            settings.spriteAlignment = (int)pivotPreset;

            if (pivotPreset == SpriteAlignment.Custom) {
                var pivotNorm = unitMode == PivotUnitMode.Pixels
                    ? PixelsToNormalized(customPivot, tex.width, tex.height)
                    : Clamp01(customPivot);

                settings.spritePivot = pivotNorm;
            }

            importer.SetTextureSettings(settings);
            return true;
        }

        private bool ApplyMultiple(TextureImporter importer, HashSet<string> selectedSpriteNames) {
            var metas = importer.spritesheet;
            if (metas == null || metas.Length == 0) {
                return false;
            }

            var filterByNames = !applyToAllSpritesInSheet && selectedSpriteNames != null &&
                                selectedSpriteNames.Count > 0;

            if (!applyToAllSpritesInSheet && !filterByNames) {
                return false;
            }

            var anyChanged = false;

            for (int i = 0; i < metas.Length; i++) {
                if (filterByNames && !selectedSpriteNames.Contains(metas[i].name)) {
                    continue;
                }

                metas[i].alignment = (int)pivotPreset;

                if (pivotPreset == SpriteAlignment.Custom) {
                    var rect = metas[i].rect;
                    var rw = Mathf.Max(1f, rect.width);
                    var rh = Mathf.Max(1f, rect.height);

                    var pivotNorm = unitMode == PivotUnitMode.Pixels
                        ? PixelsToNormalized(customPivot, rw, rh)
                        : Clamp01(customPivot);

                    metas[i].pivot = pivotNorm;
                }

                anyChanged = true;
            }

            if (!anyChanged) {
                return false;
            }

            importer.spritesheet = metas;
            return true;
        }

        private static Vector2 PixelsToNormalized(Vector2 px, float w, float h) {
            var nx = px.x / Mathf.Max(1f, w);
            var ny = px.y / Mathf.Max(1f, h);
            return Clamp01(new Vector2(nx, ny));
        }

        private static Vector2 Clamp01(Vector2 v) {
            v.x = Mathf.Clamp01(v.x);
            v.y = Mathf.Clamp01(v.y);
            return v;
        }
    }
}
