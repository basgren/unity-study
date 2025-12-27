using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Editor {
    /// <summary>
    /// Editor window for validating sprite assets against project-wide settings.
    /// Allows bulk validation and fixing of PPU, filter mode, and compression.
    /// </summary>
    public class SpriteValidatorWindow : EditorWindow {
        private const string PPU_KEY = "SpriteValidator_PPU";
        private const string FILTER_MODE_KEY = "SpriteValidator_FilterMode";
        private const string COMPRESSION_KEY = "SpriteValidator_Compression";
        private const string FOLD_OUT_KEY = "SpriteValidator_FoldOut";
        private const string SKIP_PACKAGES_KEY = "SpriteValidator_SkipPackages";
        private const string SKIPPED_DIRS_KEY = "SpriteValidator_SkippedDirs";

        private int targetPpu = 100;
        private FilterMode targetFilterMode = FilterMode.Bilinear;
        private TextureImporterCompression targetCompression = TextureImporterCompression.Uncompressed;
        private bool showParameters = true;
        private bool skipPackages = true;
        private string skippedDirectories = "";

        private List<SpriteValidationResult> invalidSprites = new List<SpriteValidationResult>();
        private Vector2 scrollPosition;

        /// <summary>
        /// Opens the Sprite Validator window from the Tools menu.
        /// </summary>
        [MenuItem("Tools/Sprite Validator")]
        public static void Open() {
            GetWindow<SpriteValidatorWindow>("Sprite Validator");
        }

        private void OnEnable() {
            LoadSettings();
        }

        /// <summary>
        /// Loads persisted settings from EditorPrefs.
        /// </summary>
        private void LoadSettings() {
            targetPpu = EditorPrefs.GetInt(PPU_KEY, 100);
            targetFilterMode = (FilterMode)EditorPrefs.GetInt(FILTER_MODE_KEY, (int)FilterMode.Bilinear);
            targetCompression = (TextureImporterCompression)EditorPrefs.GetInt(COMPRESSION_KEY, (int)TextureImporterCompression.Uncompressed);
            showParameters = EditorPrefs.GetBool(FOLD_OUT_KEY, true);
            skipPackages = EditorPrefs.GetBool(SKIP_PACKAGES_KEY, true);
            skippedDirectories = EditorPrefs.GetString(SKIPPED_DIRS_KEY, "");
        }

        /// <summary>
        /// Saves current settings to EditorPrefs.
        /// </summary>
        private void SaveSettings() {
            EditorPrefs.SetInt(PPU_KEY, targetPpu);
            EditorPrefs.SetInt(FILTER_MODE_KEY, (int)targetFilterMode);
            EditorPrefs.SetInt(COMPRESSION_KEY, (int)targetCompression);
            EditorPrefs.SetBool(FOLD_OUT_KEY, showParameters);
            EditorPrefs.SetBool(SKIP_PACKAGES_KEY, skipPackages);
            EditorPrefs.SetString(SKIPPED_DIRS_KEY, skippedDirectories);
        }

        private void OnGUI() {
            DrawParameters();
            EditorGUILayout.Space();

            if (GUILayout.Button("Validate", GUILayout.Height(30))) {
                ValidateSprites();
            }

            EditorGUILayout.Space();
            DrawResults();

            if (invalidSprites.Count > 0) {
                int selectedCount = invalidSprites.Count(s => s != null && s.IsSelected && s.SpriteAsset != null);
                string buttonText = selectedCount > 0 ? $"Fix Selected ({selectedCount})" : "Fix All";
                if (GUILayout.Button(buttonText, GUILayout.Height(30))) {
                    FixSprites(selectedCount > 0);
                }
            }
        }

        /// <summary>
        /// Renders the parameters foldout panel.
        /// </summary>
        private void DrawParameters() {
            showParameters = EditorGUILayout.BeginFoldoutHeaderGroup(showParameters, "Parameters");
            if (showParameters) {
                EditorGUI.BeginChangeCheck();
                
                targetPpu = EditorGUILayout.IntField("Pixels Per Unit", targetPpu);
                targetFilterMode = (FilterMode)EditorGUILayout.EnumPopup("Filter Mode", targetFilterMode);
                targetCompression = (TextureImporterCompression)EditorGUILayout.EnumPopup("Compression", targetCompression);
                
                EditorGUILayout.Space();
                skipPackages = EditorGUILayout.Toggle("Skip Packages", skipPackages);
                EditorGUILayout.LabelField("Skipped Directories (comma separated):", EditorStyles.miniLabel);
                skippedDirectories = EditorGUILayout.TextArea(skippedDirectories, GUILayout.MinHeight(40));

                if (EditorGUI.EndChangeCheck()) {
                    SaveSettings();
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        /// <summary>
        /// Renders the list of validation results.
        /// </summary>
        private void DrawResults() {
            // Prune deleted assets
            int initialCount = invalidSprites.Count;
            invalidSprites.RemoveAll(s => s.SpriteAsset == null);
            if (invalidSprites.Count != initialCount) {
                Repaint();
            }

            int selectedCount = invalidSprites.Count(s => s.IsSelected);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Invalid Sprites Found: {invalidSprites.Count} ({selectedCount} selected)", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Select All", EditorStyles.miniButton, GUILayout.Width(70))) {
                invalidSprites.ForEach(s => s.IsSelected = true);
            }
            if (GUILayout.Button("Deselect All", EditorStyles.miniButton, GUILayout.Width(80))) {
                invalidSprites.ForEach(s => s.IsSelected = false);
            }
            EditorGUILayout.EndHorizontal();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            bool needsRepaint = false;
            for (int i = 0; i < invalidSprites.Count; i++) {
                if (DrawSpriteResult(invalidSprites[i])) {
                    needsRepaint = true;
                }
            }

            if (needsRepaint) {
                Repaint();
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Draws an individual sprite validation result item.
        /// </summary>
        /// <param name="result">The validation result to draw.</param>
        /// <returns>True if a repaint is needed (e.g. preview still loading).</returns>
        private bool DrawSpriteResult(SpriteValidationResult result) {
            if (result.SpriteAsset == null) return false;

            bool isPreviewLoading = false;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            result.IsSelected = EditorGUILayout.Toggle(result.IsSelected, GUILayout.Width(20));
            
            // Preview
            Texture2D preview = AssetPreview.GetAssetPreview(result.SpriteAsset) ?? AssetPreview.GetMiniThumbnail(result.SpriteAsset);
            if (preview != null) {
                GUILayout.Label(preview, GUILayout.Width(64), GUILayout.Height(64));
            } else {
                GUILayout.Box("No Preview", GUILayout.Width(64), GUILayout.Height(64));
                if (AssetPreview.IsLoadingAssetPreview(result.SpriteAsset.GetInstanceID())) {
                    isPreviewLoading = true;
                }
            }

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(result.SpriteAsset.name, EditorStyles.boldLabel);
            
            foreach (var error in result.Errors) {
                EditorGUILayout.LabelField($"- {error}", EditorStyles.miniLabel);
            }
            
            EditorGUILayout.EndVertical();
            
            if (GUILayout.Button("Ping", GUILayout.Width(50))) {
                EditorGUIUtility.PingObject(result.SpriteAsset);
            }

            EditorGUILayout.EndHorizontal();
            return isPreviewLoading;
        }

        /// <summary>
        /// Scans the project for sprites that do not match the target parameters.
        /// </summary>
        private void ValidateSprites() {
            invalidSprites.Clear();
            string[] guids = AssetDatabase.FindAssets("t:Texture2D");

            string[] skipDirs = skippedDirectories.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim().Replace('\\', '/'))
                .Where(d => !string.IsNullOrEmpty(d))
                .ToArray();

            foreach (string guid in guids) {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (skipPackages && path.StartsWith("Packages/")) {
                    continue;
                }

                if (skipDirs.Any(dir => path.StartsWith(dir, StringComparison.OrdinalIgnoreCase))) {
                    continue;
                }

                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer == null || importer.textureType != TextureImporterType.Sprite) {
                    continue;
                }

                List<string> errors = new List<string>();

                if (Mathf.Abs(importer.spritePixelsPerUnit - targetPpu) > 0.001f) {
                    errors.Add($"PPU mismatch: {importer.spritePixelsPerUnit} (expected {targetPpu})");
                }

                if (importer.filterMode != targetFilterMode) {
                    errors.Add($"Filter Mode mismatch: {importer.filterMode} (expected {targetFilterMode})");
                }

                if (importer.textureCompression != targetCompression) {
                    errors.Add($"Compression mismatch: {importer.textureCompression} (expected {targetCompression})");
                }

                if (errors.Count > 0) {
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    invalidSprites.Add(new SpriteValidationResult {
                        SpriteAsset = texture,
                        AssetPath = path,
                        Errors = errors
                    });
                }
            }
        }

        /// <summary>
        /// Applies the target parameters to the invalid sprites.
        /// </summary>
        /// <param name="selectedOnly">If true, only fixes sprites that are currently selected in the list.</param>
        private void FixSprites(bool selectedOnly) {
            var targets = selectedOnly ? invalidSprites.Where(s => s.IsSelected) : invalidSprites;
            foreach (var result in targets) {
                if (result.SpriteAsset == null) continue;

                TextureImporter importer = AssetImporter.GetAtPath(result.AssetPath) as TextureImporter;
                if (importer == null) continue;

                importer.spritePixelsPerUnit = targetPpu;
                importer.filterMode = targetFilterMode;
                importer.textureCompression = targetCompression;

                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }

            ValidateSprites();
        }

        /// <summary>
        /// Data structure representing a sprite that failed validation.
        /// </summary>
        private class SpriteValidationResult {
            public bool IsSelected;
            public Texture2D SpriteAsset;
            public string AssetPath;
            public List<string> Errors;
        }
    }
}
