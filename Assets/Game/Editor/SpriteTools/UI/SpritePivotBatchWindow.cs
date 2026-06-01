// SpritePivotBatchWindow.cs
// Sprite Tools — UI Toolkit batch pivot + naming window.

using System;
using System.Collections.Generic;
using System.IO;
using Game.Editor.SpriteTools.Core;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Editor.SpriteTools.UI {
    /// <summary>
    /// UI Toolkit front-end for the Sprite Tools batch operations. Loads a UXML layout, binds its static
    /// controls to this window's serialized fields, renders the dynamic per-row list by hand, and delegates
    /// every asset edit to <see cref="SpriteImportOps"/> / <see cref="SpriteSheetRows"/> /
    /// <see cref="SpriteNaming"/>. The window holds UI state only; it contains no import logic.
    /// </summary>
    public sealed class SpritePivotBatchWindow : EditorWindow {
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

        /// <summary>Per-row UI state: replaces the old three index-parallel lists with one typed entry.</summary>
        [Serializable]
        private sealed class RowConfig {
            public string name;
            public SpriteAlignment alignment;
            public Vector2 customPivot;
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

        // UI assets are loaded by path: the window is code-instantiated, so there is no inspector to wire
        // serialized VisualTreeAsset references on.
        private const string UiFolder = "Assets/Game/Editor/SpriteTools/UI/";
        private const string WindowUxmlPath = UiFolder + "SpritePivotBatchWindow.uxml";
        private const string RowUxmlPath = UiFolder + "RowView.uxml";

        // --- Serialized state (bound via SerializedObject / binding-path) --------------------------------
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

        [SerializeField]
        private List<RowConfig> rows = new List<RowConfig>();

        // Per-row cell cache, rebuilt only when the selected texture changes (row detection loads sub-assets).
        private string cachedRowTexturePath;
        private List<List<SpriteCell>> cachedRows;

        // --- UI element references ----------------------------------------------------------------------
        private SerializedObject serialized;
        private VisualTreeAsset rowTemplate;
        private Label modeLabel;
        private VisualElement eachRowOwnPivotContainer;
        private VisualElement pivotSection;
        private EnumField pivotPresetField;
        private VisualElement customPivotRow;
        private VisualElement flatNameSection;
        private VisualElement rowsSection;
        private VisualElement rowsContainer;
        private VisualElement numberingSection;
        private IntegerField padWidthField;
        private Button applyButton;
        private VisualElement previewContainer;

        private bool IsPixels => unitMode == PivotUnitMode.Pixels;

        [MenuItem("Tools/Sprites/Batch Pivot (Sprite Editor Style)")]
        public static void Open() {
            var wnd = GetWindow<SpritePivotBatchWindow>("Batch Sprite Pivot");
            wnd.minSize = new Vector2(480f, 440f);
            wnd.Show();
        }

        private void CreateGUI() {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(WindowUxmlPath);
            if (tree == null) {
                rootVisualElement.Add(new HelpBox($"Missing UXML at {WindowUxmlPath}", HelpBoxMessageType.Error));
                return;
            }

            tree.CloneTree(rootVisualElement);
            rowTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(RowUxmlPath);

            QueryElements();

            serialized = new SerializedObject(this);
            rootVisualElement.Bind(serialized);

            // One callback drives every live update when a bound control changes.
            rootVisualElement.TrackSerializedObjectValue(serialized, _ => Refresh());

            // Pad width is non-negative, mirroring the old Mathf.Max(0, ...) clamp.
            // Clamp without re-notifying (setting .value here would re-enter this callback).
            padWidthField.RegisterValueChangedCallback(evt => {
                if (evt.newValue < 0) {
                    padWidthField.SetValueWithoutNotify(0);
                    renamePadWidth = 0;
                    serialized.Update();
                }
            });

            applyButton.clicked += OnApplyClicked;
            Refresh();
        }

        private void QueryElements() {
            modeLabel = rootVisualElement.Q<Label>("mode-label");
            eachRowOwnPivotContainer = rootVisualElement.Q<VisualElement>("each-row-own-pivot");
            pivotSection = rootVisualElement.Q<VisualElement>("pivot-section");
            pivotPresetField = rootVisualElement.Q<EnumField>("pivot-preset");
            customPivotRow = rootVisualElement.Q<VisualElement>("custom-pivot-row");
            flatNameSection = rootVisualElement.Q<VisualElement>("flat-name-section");
            rowsSection = rootVisualElement.Q<VisualElement>("rows-section");
            rowsContainer = rootVisualElement.Q<VisualElement>("rows-container");
            numberingSection = rootVisualElement.Q<VisualElement>("numbering-section");
            padWidthField = rootVisualElement.Q<IntegerField>("pad-width");
            applyButton = rootVisualElement.Q<Button>("apply-button");
            previewContainer = rootVisualElement.Q<VisualElement>("preview-container");
        }

        private void OnSelectionChange() {
            Refresh();
        }

        // --- View synchronization -----------------------------------------------------------------------

        // Single source of truth for derived view state: mode banner, section visibility/enable, the rows
        // list, and the live preview. Safe to call before the UI exists (early Selection callbacks).
        private void Refresh() {
            if (modeLabel == null) {
                return;
            }

            var mode = DetermineMode(out var perRowPath);
            if (mode == RenameMode.PerRow) {
                EnsureRowCache(perRowPath);
            }

            var rowOwnPivot = mode == RenameMode.PerRow && changePivot && eachRowOwnPivot;

            modeLabel.text = ModeText(mode, perRowPath);

            SetDisplay(eachRowOwnPivotContainer, mode == RenameMode.PerRow && changePivot);

            // Shared pivot block: hidden in per-row "own pivot" mode (each row carries its own).
            var showSharedPivot = mode != RenameMode.None && changePivot
                && !(mode == RenameMode.PerRow && eachRowOwnPivot);
            SetDisplay(pivotSection, showSharedPivot);
            pivotPresetField.label = mode == RenameMode.PerRow ? "Sheet Pivot" : "Pivot";
            customPivotRow.SetEnabled(pivotPreset == SpriteAlignment.Custom);

            SetDisplay(flatNameSection, mode == RenameMode.Flat && changeNames);

            var showRows = mode == RenameMode.PerRow && (changeNames || rowOwnPivot);
            SetDisplay(rowsSection, showRows);
            if (showRows) {
                RebuildRows(rowOwnPivot);
            }

            SetDisplay(numberingSection, mode != RenameMode.None && changeNames);

            applyButton.SetEnabled((changePivot || changeNames) && mode != RenameMode.None);

            RebuildPreview(mode, rowOwnPivot);
        }

        private static void SetDisplay(VisualElement element, bool visible) {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Renders one cloned RowView per detected row. Name/X/Y/unit edits only refresh the preview so the
        // focused field is not destroyed mid-edit; a preset change calls Refresh to re-toggle enable states.
        private void RebuildRows(bool rowOwnPivot) {
            rowsContainer.Clear();
            if (cachedRows == null || rowTemplate == null) {
                return;
            }

            var count = Mathf.Min(cachedRows.Count, rows.Count);
            for (int r = 0; r < count; r++) {
                var index = r;
                var cfg = rows[index];

                var rowElem = rowTemplate.CloneTree();
                var label = rowElem.Q<Label>("row-label");
                var nameField = rowElem.Q<TextField>("row-name");
                var pivotLine = rowElem.Q<VisualElement>("row-pivot-line");
                var presetField = rowElem.Q<EnumField>("row-preset");
                var unitField = rowElem.Q<EnumField>("row-unit");
                var xField = rowElem.Q<FloatField>("row-x");
                var yField = rowElem.Q<FloatField>("row-y");

                label.text = $"Row {index} ({cachedRows[index].Count})";

                nameField.SetEnabled(changeNames);
                nameField.SetValueWithoutNotify(cfg.name);
                nameField.RegisterValueChangedCallback(evt => {
                    rows[index].name = evt.newValue;
                    RefreshPreview();
                });

                SetDisplay(pivotLine, rowOwnPivot);
                if (rowOwnPivot) {
                    presetField.Init(cfg.alignment);
                    presetField.SetValueWithoutNotify(cfg.alignment);
                    presetField.RegisterValueChangedCallback(evt => {
                        rows[index].alignment = (SpriteAlignment)evt.newValue;
                        Refresh();
                    });

                    var customEnabled = cfg.alignment == SpriteAlignment.Custom;

                    unitField.Init(unitMode);
                    unitField.SetValueWithoutNotify(unitMode);
                    unitField.SetEnabled(customEnabled);
                    unitField.RegisterValueChangedCallback(evt => {
                        // unitMode is shared by all rows, so rebuild every row to keep the
                        // sibling unit fields in sync (full Refresh, not just the preview).
                        unitMode = (PivotUnitMode)evt.newValue;
                        serialized.Update();
                        Refresh();
                    });

                    xField.SetEnabled(customEnabled);
                    yField.SetEnabled(customEnabled);
                    xField.SetValueWithoutNotify(cfg.customPivot.x);
                    yField.SetValueWithoutNotify(cfg.customPivot.y);
                    xField.RegisterValueChangedCallback(evt => {
                        rows[index].customPivot = new Vector2(evt.newValue, rows[index].customPivot.y);
                        RefreshPreview();
                    });
                    yField.RegisterValueChangedCallback(evt => {
                        rows[index].customPivot = new Vector2(rows[index].customPivot.x, evt.newValue);
                        RefreshPreview();
                    });
                }

                rowsContainer.Add(rowElem);
            }
        }

        // Recomputes mode and refreshes only the preview (used by row edits that must not rebuild the rows).
        private void RefreshPreview() {
            var mode = DetermineMode(out _);
            var rowOwnPivot = mode == RenameMode.PerRow && changePivot && eachRowOwnPivot;
            RebuildPreview(mode, rowOwnPivot);
        }

        private void RebuildPreview(RenameMode mode, bool rowOwnPivot) {
            previewContainer.Clear();

            if (mode == RenameMode.None) {
                previewContainer.Add(MakePreviewLine("—", string.Empty));
                return;
            }

            if (mode == RenameMode.Flat) {
                previewContainer.Add(MakePreviewLine("Pivot",
                    changePivot ? PivotPreviewText(pivotPreset, customPivot) : "(unchanged)"));

                var selected = CollectSelectedSpritesOrdered();
                for (int i = 0; i < selected.Count; i++) {
                    previewContainer.Add(MakePreviewLine(selected[i].currentName,
                        changeNames ? "→  " + BuildName(renameBaseName, i) : "(unchanged)"));
                }

                return;
            }

            if (cachedRows == null) {
                return;
            }

            var count = Mathf.Min(cachedRows.Count, rows.Count);
            for (int r = 0; r < count; r++) {
                string pivotText;
                if (!changePivot) {
                    pivotText = "(unchanged)";
                } else if (rowOwnPivot) {
                    pivotText = PivotPreviewText(rows[r].alignment, rows[r].customPivot);
                } else {
                    pivotText = PivotPreviewText(pivotPreset, customPivot);
                }

                var header = MakePreviewLine($"Row {r} — {rows[r].name}", pivotText);
                header.AddToClassList("preview-row-header");
                previewContainer.Add(header);

                var row = cachedRows[r];
                for (int c = 0; c < row.Count; c++) {
                    previewContainer.Add(MakePreviewLine("    " + row[c].Name,
                        changeNames ? "→  " + BuildName(rows[r].name, c) : "(unchanged)"));
                }
            }
        }

        private static VisualElement MakePreviewLine(string left, string right) {
            var line = new VisualElement();
            line.AddToClassList("preview-line");

            var leftLabel = new Label(left);
            leftLabel.AddToClassList("preview-left");
            line.Add(leftLabel);

            var rightLabel = new Label(right);
            rightLabel.AddToClassList("preview-right");
            line.Add(rightLabel);

            return line;
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

        // --- Apply --------------------------------------------------------------------------------------

        private void OnApplyClicked() {
            var mode = DetermineMode(out var perRowPath);
            Apply(mode, perRowPath);
            Refresh();
        }

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
            var count = Mathf.Min(cachedRows.Count, rows.Count);
            for (int r = 0; r < count; r++) {
                var setting = new PivotSetting(rows[r].alignment, rows[r].customPivot);
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
            var count = Mathf.Min(cachedRows.Count, rows.Count);
            for (int r = 0; r < count; r++) {
                var rowName = rows[r].name;
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

        // --- Selection / row cache ----------------------------------------------------------------------

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
        // A (re)built sheet gets fresh suggestions: each row name defaults to its sprites' shared prefix, and
        // each row's pivot defaults to the current global pivot.
        private void EnsureRowCache(string texturePath) {
            if (texturePath == cachedRowTexturePath && cachedRows != null) {
                return;
            }

            cachedRowTexturePath = texturePath;
            cachedRows = SpriteSheetRows.Detect(texturePath);

            rows.Clear();
            for (int r = 0; r < cachedRows.Count; r++) {
                rows.Add(new RowConfig {
                    name = SpriteSheetRows.DeriveRowName(cachedRows[r]),
                    alignment = pivotPreset,
                    customPivot = customPivot
                });
            }
        }
    }
}
