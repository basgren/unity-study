// SpritePivotBatchTool.cs
// Sprite Tools — batch pivot + naming window.

using System.Collections.Generic;
using System.IO;
using Game.Editor.SpriteTools.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.SpriteTools {
    /// <summary>
    /// IMGUI front-end for the Sprite Tools batch operations. Holds UI state, reads the Project-window
    /// selection, and delegates all asset edits to <see cref="SpriteImportOps"/> / <see cref="SpriteSheetRows"/>
    /// / <see cref="SpriteNaming"/> — the window contains no import logic itself, so the UI can be swapped
    /// (e.g. to UI Toolkit) without touching the core.
    /// </summary>
    /// <remarks>
    /// The layout is driven by the current selection ("Mode", shown at the top):
    /// <list type="bullet">
    /// <item><description><b>Flat</b> (Sprite sub-assets selected): one pivot and a single base name numbered in selection order.</description></item>
    /// <item><description><b>Per-row</b> (a whole texture selected): rows are auto-detected; each gets its own name, and optionally its own pivot.</description></item>
    /// </list>
    /// Two checkboxes (<c>Change Pivot</c>, <c>Rename</c>) gate which operations a single Apply performs.
    /// Import setting changes may not be reliably undoable — prefer version control for safety.
    /// </remarks>
    public sealed class SpritePivotBatchTool : EditorWindow {
        private enum PivotUnitMode {
            Normalized,
            Pixels
        }

        /// <summary>How the current selection is interpreted.</summary>
        private enum RenameMode {
            None,
            Flat,
            PerRow
        }

        /// <summary>One selected Sprite sub-asset, captured in Selection order for flat rename/preview.</summary>
        private readonly struct SelectedSprite {
            public readonly string assetPath;
            public readonly string currentName;

            public SelectedSprite(string assetPath, string currentName) {
                this.assetPath = assetPath;
                this.currentName = currentName;
            }
        }

        [SerializeField]
        private bool changePivot = true;

        [SerializeField]
        private SpriteAlignment pivotPreset = SpriteAlignment.Center;

        [SerializeField]
        private PivotUnitMode unitMode = PivotUnitMode.Pixels;

        [SerializeField]
        private Vector2 customPivot = new Vector2(16f, 0f);

        [SerializeField]
        private bool eachRowOwnPivot;

        [SerializeField]
        private bool changeNames;

        [SerializeField]
        private string renameBaseName = "sprite";

        [SerializeField]
        private int renameStartIndex;

        [SerializeField]
        private int renamePadWidth = 2;

        private Vector2 previewScroll;

        // Per-row state, rebuilt only when the selected texture changes (row detection loads sub-assets).
        private string cachedRowTexturePath;
        private List<List<SpriteCell>> cachedRows;
        private readonly List<string> rowNames = new List<string>();
        private readonly List<SpriteAlignment> rowPivotPresets = new List<SpriteAlignment>();
        private readonly List<Vector2> rowCustomPivots = new List<Vector2>();

        private bool IsPixels => unitMode == PivotUnitMode.Pixels;

        [MenuItem("Tools/Sprites/Batch Pivot (Sprite Editor Style)")]
        public static void Open() {
            var wnd = GetWindow<SpritePivotBatchTool>("Batch Sprite Pivot");
            wnd.minSize = new Vector2(480f, 440f);
            wnd.Show();
        }

        private void OnSelectionChange() {
            // Mode, per-row fields and preview reflect the current Project-window selection live.
            Repaint();
        }

        private void OnGUI() {
            var mode = DetermineMode(out var perRowPath);
            if (mode == RenameMode.PerRow) {
                EnsureRowCache(perRowPath);
            }

            EditorGUILayout.LabelField("Mode", ModeText(mode, perRowPath), EditorStyles.boldLabel);
            EditorGUILayout.Space();

            changePivot = EditorGUILayout.ToggleLeft("Change Pivot", changePivot);
            if (mode == RenameMode.PerRow && changePivot) {
                EditorGUI.indentLevel++;
                eachRowOwnPivot = EditorGUILayout.ToggleLeft("Each row with own pivot", eachRowOwnPivot);
                EditorGUI.indentLevel--;
            }

            changeNames = EditorGUILayout.ToggleLeft("Rename", changeNames);

            var rowOwnPivot = mode == RenameMode.PerRow && changePivot && eachRowOwnPivot;
            EditorGUILayout.Space();

            if (mode == RenameMode.None) {
                EditorGUILayout.HelpBox("Select Sprite sub-assets (flat) or a whole texture (per-row).",
                    MessageType.None);
            } else if (mode == RenameMode.Flat) {
                if (changePivot) {
                    DrawPivotPresetAndCustom("Pivot", ref pivotPreset, ref customPivot);
                }

                if (changeNames) {
                    renameBaseName = EditorGUILayout.TextField("Base Name", renameBaseName);
                    DrawNumbering();
                }
            } else {
                if (changePivot && !eachRowOwnPivot) {
                    DrawPivotPresetAndCustom("Sheet Pivot", ref pivotPreset, ref customPivot);
                }

                if (changeNames || rowOwnPivot) {
                    EditorGUILayout.LabelField("Rows", EditorStyles.boldLabel);
                    for (int r = 0; r < cachedRows.Count; r++) {
                        DrawRowLine(r, rowOwnPivot);
                    }
                }

                if (changeNames) {
                    DrawNumbering();
                }
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!changePivot && !changeNames)) {
                if (GUILayout.Button("Apply")) {
                    Apply(mode, perRowPath);
                }
            }

            EditorGUILayout.Space();
            DrawSelectionPreview(mode, rowOwnPivot);

            EditorGUILayout.HelpBox(
                "Mode follows selection: Sprite sub-assets → flat (selection-order) rename; a whole texture → per-row.\n" +
                "Custom pivot is interpreted from the bottom-left of each sprite rect, like Sprite Editor.",
                MessageType.Info
            );
        }

        // --- UI helpers ------------------------------------------------------------------------------------

        private static string ModeText(RenameMode mode, string perRowPath) {
            switch (mode) {
                case RenameMode.Flat:
                    return $"Flat selection ({CollectSelectedSpritesOrdered().Count} sprites)";
                case RenameMode.PerRow:
                    return $"Per-row — {Path.GetFileName(perRowPath)}";
                default:
                    return "Nothing selected";
            }
        }

        private void DrawPivotPresetAndCustom(string label, ref SpriteAlignment preset, ref Vector2 custom) {
            preset = (SpriteAlignment)EditorGUILayout.EnumPopup(label, preset);

            using (new EditorGUI.DisabledScope(preset != SpriteAlignment.Custom)) {
                // Unit dropdown + custom X + Y on one line.
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Custom Pivot");
                unitMode = (PivotUnitMode)EditorGUILayout.EnumPopup(unitMode, GUILayout.Width(90f));

                var prev = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 14f;
                var x = EditorGUILayout.FloatField("X", custom.x);
                var y = EditorGUILayout.FloatField("Y", custom.y);
                EditorGUIUtility.labelWidth = prev;
                custom = new Vector2(x, y);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawRowLine(int r, bool rowOwnPivot) {
            var row = cachedRows[r];

            // Line 1: row label + name.
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Row {r} ({row.Count})", GUILayout.Width(110f));
            using (new EditorGUI.DisabledScope(!changeNames)) {
                rowNames[r] = EditorGUILayout.TextField(rowNames[r], GUILayout.MinWidth(80f));
            }

            EditorGUILayout.EndHorizontal();

            if (!rowOwnPivot) {
                return;
            }

            // Line 2 (indented): this row's pivot — preset + unit + X + Y. Unit/X/Y are editable only
            // when the preset is Custom.
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(15f);
            EditorGUILayout.LabelField("Pivot", GUILayout.Width(40f));
            rowPivotPresets[r] = (SpriteAlignment)EditorGUILayout.EnumPopup(rowPivotPresets[r], GUILayout.Width(120f));

            using (new EditorGUI.DisabledScope(rowPivotPresets[r] != SpriteAlignment.Custom)) {
                unitMode = (PivotUnitMode)EditorGUILayout.EnumPopup(unitMode, GUILayout.Width(80f));

                var prev = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 12f;
                var x = EditorGUILayout.FloatField("X", rowCustomPivots[r].x, GUILayout.Width(50f));
                var y = EditorGUILayout.FloatField("Y", rowCustomPivots[r].y, GUILayout.Width(50f));
                EditorGUIUtility.labelWidth = prev;
                rowCustomPivots[r] = new Vector2(x, y);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawNumbering() {
            renameStartIndex = EditorGUILayout.IntField("Start Index", renameStartIndex);
            renamePadWidth = Mathf.Max(0, EditorGUILayout.IntField("Pad Width", renamePadWidth));
        }

        private void DrawSelectionPreview(RenameMode mode, bool rowOwnPivot) {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            if (mode == RenameMode.None) {
                EditorGUILayout.LabelField("—");
                return;
            }

            previewScroll = EditorGUILayout.BeginScrollView(previewScroll, GUILayout.MaxHeight(180f));

            if (mode == RenameMode.Flat) {
                EditorGUILayout.LabelField("Pivot",
                    changePivot ? PivotPreviewText(pivotPreset, customPivot) : "(unchanged)");

                var selected = CollectSelectedSpritesOrdered();
                for (int i = 0; i < selected.Count; i++) {
                    EditorGUILayout.LabelField(selected[i].currentName,
                        changeNames ? "→  " + BuildName(renameBaseName, i) : "(unchanged)");
                }
            } else {
                for (int r = 0; r < cachedRows.Count; r++) {
                    string pivotText;
                    if (!changePivot) {
                        pivotText = "(unchanged)";
                    } else if (rowOwnPivot) {
                        pivotText = PivotPreviewText(rowPivotPresets[r], rowCustomPivots[r]);
                    } else {
                        pivotText = PivotPreviewText(pivotPreset, customPivot);
                    }

                    EditorGUILayout.LabelField($"Row {r} — {rowNames[r]}", pivotText, EditorStyles.miniBoldLabel);

                    var row = cachedRows[r];
                    for (int c = 0; c < row.Count; c++) {
                        EditorGUILayout.LabelField("    " + row[c].Name,
                            changeNames ? "→  " + BuildName(rowNames[r], c) : "(unchanged)");
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private string PivotPreviewText(SpriteAlignment preset, Vector2 custom) {
            if (preset != SpriteAlignment.Custom) {
                return preset.ToString();
            }

            var unit = IsPixels ? "px" : "normalized";
            return $"Custom ({custom.x}, {custom.y}) {unit}";
        }

        private string BuildName(string baseName, int offset) {
            return SpriteNaming.BuildIndexedName(baseName, renameStartIndex, offset, renamePadWidth);
        }

        // --- Apply -----------------------------------------------------------------------------------------

        private void Apply(RenameMode mode, string perRowPath) {
            // Pivot first: rename changes sprite names, which would otherwise break the pivot pass's
            // name-based matching.
            if (changePivot) {
                if (mode == RenameMode.PerRow && eachRowOwnPivot) {
                    ApplyPerRowPivot(perRowPath);
                } else {
                    ApplyUniformPivot();
                }
            }

            if (changeNames) {
                if (mode == RenameMode.Flat) {
                    RenameFlat();
                } else if (mode == RenameMode.PerRow) {
                    RenamePerRow(perRowPath);
                } else {
                    Debug.LogWarning("Rename skipped: select Sprite sub-assets (flat) or a whole texture (per-row).");
                }

                // Names changed on disk; drop the row cache so the UI/preview rebuild from the new names.
                cachedRowTexturePath = null;
            }
        }

        // Flat selection (filter by selected sprite names) or whole-texture selection (all sprites).
        private void ApplyUniformPivot() {
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
                    if (SpriteImportOps.ApplyUniformPivot(kv.Key, pivotPreset, customPivot, IsPixels, kv.Value)) {
                        changed++;
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

        private void ApplyPerRowPivot(string texturePath) {
            EnsureRowCache(texturePath);
            if (cachedRows == null || cachedRows.Count == 0) {
                Debug.LogWarning("Per-row pivot skipped: no rows detected.");
                return;
            }

            var pivotByName = new Dictionary<string, PivotSetting>();
            for (int r = 0; r < cachedRows.Count; r++) {
                var setting = new PivotSetting(rowPivotPresets[r], rowCustomPivots[r]);
                var row = cachedRows[r];
                for (int c = 0; c < row.Count; c++) {
                    pivotByName[row[c].Name] = setting;
                }
            }

            AssetDatabase.StartAssetEditing();
            try {
                SpriteImportOps.ApplyPivots(texturePath, pivotByName, IsPixels);
            }
            finally {
                AssetDatabase.StopAssetEditing();
            }

            Debug.Log($"Per-row pivot applied to {cachedRows.Count} rows.");
        }

        private void RenameFlat() {
            if (string.IsNullOrEmpty(renameBaseName)) {
                Debug.LogWarning("Rename skipped: Base Name is empty.");
                return;
            }

            var selected = CollectSelectedSpritesOrdered();
            if (selected.Count == 0) {
                Debug.LogWarning("Rename skipped: no Sprite sub-assets selected.");
                return;
            }

            // Group renames per texture while numbering from the global selection order, so a selection
            // within one sheet reads as one continuous sequence.
            var renamesByPath = new Dictionary<string, Dictionary<string, string>>();
            for (int i = 0; i < selected.Count; i++) {
                var newName = BuildName(renameBaseName, i);
                if (!renamesByPath.TryGetValue(selected[i].assetPath, out var map)) {
                    map = new Dictionary<string, string>();
                    renamesByPath[selected[i].assetPath] = map;
                }

                map[selected[i].currentName] = newName;
            }

            RunRenames(renamesByPath);
        }

        private void RenamePerRow(string texturePath) {
            EnsureRowCache(texturePath);
            if (cachedRows == null || cachedRows.Count == 0) {
                Debug.LogWarning("Rename skipped: no rows detected on the selected texture.");
                return;
            }

            var map = new Dictionary<string, string>();
            for (int r = 0; r < cachedRows.Count; r++) {
                var rowName = rowNames[r];
                if (string.IsNullOrEmpty(rowName)) {
                    Debug.LogWarning($"Rename skipped: Row {r} name is empty.");
                    return;
                }

                var row = cachedRows[r];
                for (int c = 0; c < row.Count; c++) {
                    // Numbering restarts per row, so every row reads as <rowName>_00.._0N left-to-right.
                    map[row[c].Name] = BuildName(rowName, c);
                }
            }

            RunRenames(new Dictionary<string, Dictionary<string, string>> { { texturePath, map } });
        }

        private static void RunRenames(Dictionary<string, Dictionary<string, string>> renamesByPath) {
            var changed = 0;
            var skipped = 0;

            AssetDatabase.StartAssetEditing();
            try {
                foreach (var kv in renamesByPath) {
                    if (SpriteImportOps.Rename(kv.Key, kv.Value)) {
                        changed++;
                    } else {
                        skipped++;
                    }
                }
            }
            finally {
                AssetDatabase.StopAssetEditing();
            }

            Debug.Log($"Batch rename finished. Textures changed: {changed}, skipped: {skipped}");
        }

        // --- Selection / row cache -------------------------------------------------------------------------

        private static RenameMode DetermineMode(out string perRowTexturePath) {
            perRowTexturePath = null;

            // Explicitly selected sub-sprites take priority: that is the flat case.
            if (CollectSelectedSpritesOrdered().Count > 0) {
                return RenameMode.Flat;
            }

            var path = GetSelectedTexturePath();
            if (!string.IsNullOrEmpty(path)) {
                perRowTexturePath = path;
                return RenameMode.PerRow;
            }

            return RenameMode.None;
        }

        // Selected Sprite sub-assets in Selection order (whole-texture selections are ignored here: they
        // have no per-sprite order and instead drive per-row mode).
        private static List<SelectedSprite> CollectSelectedSpritesOrdered() {
            var result = new List<SelectedSprite>();

            var objects = Selection.objects;
            if (objects == null || objects.Length == 0) {
                return result;
            }

            for (int i = 0; i < objects.Length; i++) {
                var sprite = objects[i] as Sprite;
                if (sprite == null) {
                    continue;
                }

                var path = AssetDatabase.GetAssetPath(sprite);
                if (string.IsNullOrEmpty(path)) {
                    continue;
                }

                result.Add(new SelectedSprite(path, sprite.name));
            }

            return result;
        }

        // Pivot targets: each selected texture/sprite path mapped to the selected sprite names (null = whole sheet).
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
                    // A whole-texture selection: null set means "all sprites in the sheet".
                    if (!result.ContainsKey(path)) {
                        result[path] = null;
                    }
                }
            }

            return result;
        }

        // The whole-texture selection for per-row mode: prefer the active object, else the first selected texture.
        private static string GetSelectedTexturePath() {
            var active = Selection.activeObject as Texture2D;
            if (active != null) {
                var p = AssetDatabase.GetAssetPath(active);
                if (!string.IsNullOrEmpty(p)) {
                    return p;
                }
            }

            var objects = Selection.objects;
            if (objects != null) {
                for (int i = 0; i < objects.Length; i++) {
                    var tex = objects[i] as Texture2D;
                    if (tex != null) {
                        var p = AssetDatabase.GetAssetPath(tex);
                        if (!string.IsNullOrEmpty(p)) {
                            return p;
                        }
                    }
                }
            }

            return null;
        }

        // Rebuilds the per-row cache only when the target texture changes (row detection loads all sub-assets).
        private void EnsureRowCache(string texturePath) {
            if (texturePath == cachedRowTexturePath && cachedRows != null) {
                return;
            }

            cachedRowTexturePath = texturePath;
            cachedRows = SpriteSheetRows.Detect(texturePath);

            // A (re)built sheet gets fresh suggestions: each row name defaults to its sprites' shared
            // prefix, and each row's pivot defaults to the current global pivot.
            rowNames.Clear();
            rowPivotPresets.Clear();
            rowCustomPivots.Clear();
            for (int r = 0; r < cachedRows.Count; r++) {
                rowNames.Add(SpriteSheetRows.DeriveRowName(cachedRows[r]));
                rowPivotPresets.Add(pivotPreset);
                rowCustomPivots.Add(customPivot);
            }
        }
    }
}
