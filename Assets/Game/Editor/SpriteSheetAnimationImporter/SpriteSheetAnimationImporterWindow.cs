using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.SpriteSheetAnimationImporter {
    /// <summary>
    /// Editor window that walks the user through slicing a sprite sheet and generating clips + controller.
    /// One-off, manual tool - no saved presets, no AssetPostprocessor. If the result is wrong the user
    /// tweaks parameters and clicks Process again.
    /// </summary>
    public sealed class SpriteSheetAnimationImporterWindow : EditorWindow {
        private const string WindowTitle = "Sprite Sheet Animation Importer";

        private SpriteSheetImportSettings settings = new SpriteSheetImportSettings();

        private Vector2 scroll;
        private ValidationReport lastReport;
        private ImportSummary lastPreview;
        private string lastMessage;
        private MessageType lastMessageType = MessageType.Info;

        [MenuItem("Tools/Sprites/Sprite Sheet Animation Importer")]
        public static void Open() {
            var wnd = GetWindow<SpriteSheetAnimationImporterWindow>(WindowTitle);
            wnd.minSize = new Vector2(460f, 560f);
            wnd.Show();
        }

        private void OnGUI() {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            DrawInstructions();
            EditorGUILayout.Space();

            DrawSourceSection();
            EditorGUILayout.Space();

            DrawSlicingSection();
            EditorGUILayout.Space();

            DrawNamingSection();
            EditorGUILayout.Space();

            DrawPivotSection();
            EditorGUILayout.Space();

            DrawAnimationSection();
            EditorGUILayout.Space();

            DrawDraftTransitionsSection();
            EditorGUILayout.Space();

            DrawActionsSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawInstructions() {
            EditorGUILayout.HelpBox(
                "Recommended order:\n" +
                "1. Select the sprite sheet texture.\n" +
                "2. Set rows, columns, and cell size.\n" +
                "3. Enter state names in row order.\n" +
                "4. Adjust pivot if needed.\n" +
                "5. Choose whether to generate clips and controller.\n" +
                "6. Optionally enter draft transition targets for each state.\n" +
                "7. Click Process. If the result is wrong, tweak parameters and run Process again.",
                MessageType.None);
        }

        private void DrawSourceSection() {
            EditorGUILayout.LabelField("1. Source", EditorStyles.boldLabel);

            var prevTexture = settings.sourceTexture;
            settings.sourceTexture = (Texture2D)EditorGUILayout.ObjectField(
                "Source Texture", settings.sourceTexture, typeof(Texture2D), false);

            if (settings.sourceTexture != prevTexture) {
                AutoFillDefaultsForNewTexture();
            }

            EditorGUILayout.BeginHorizontal();
            settings.outputFolder = EditorGUILayout.TextField("Output Folder", settings.outputFolder);
            if (GUILayout.Button("Pick", GUILayout.Width(60f))) {
                PickOutputFolder();
            }

            EditorGUILayout.EndHorizontal();

            if (settings.sourceTexture != null) {
                var resolved = SpriteSheetImportProcessor.ResolveOutputFolder(settings);
                EditorGUILayout.LabelField("Will write to", resolved);
            }

            EditorGUILayout.HelpBox(
                "Start by picking the sprite sheet texture. Leave the output folder empty and the tool " +
                "will create a subfolder next to the texture named after the controller, " +
                "keeping each sheet's generated assets isolated. You can move the folder afterwards.",
                MessageType.Info);
        }

        private void DrawSlicingSection() {
            EditorGUILayout.LabelField("2. Slicing", EditorStyles.boldLabel);

            settings.rows = Mathf.Max(1, EditorGUILayout.IntField("Rows", settings.rows));
            settings.columns = Mathf.Max(1, EditorGUILayout.IntField("Columns", settings.columns));

            EditorGUILayout.LabelField("Margin (px)");
            EditorGUI.indentLevel++;
            settings.marginX = Mathf.Max(0, EditorGUILayout.IntField("X", settings.marginX));
            settings.marginY = Mathf.Max(0, EditorGUILayout.IntField("Y", settings.marginY));
            EditorGUI.indentLevel--;

            EditorGUILayout.LabelField("Spacing (px)");
            EditorGUI.indentLevel++;
            settings.spacingX = Mathf.Max(0, EditorGUILayout.IntField("X", settings.spacingX));
            settings.spacingY = Mathf.Max(0, EditorGUILayout.IntField("Y", settings.spacingY));
            EditorGUI.indentLevel--;

            settings.rowOrder = (RowOrder)EditorGUILayout.EnumPopup("Row Order", settings.rowOrder);
            settings.skipEmptyCells = EditorGUILayout.Toggle("Skip Empty Cells", settings.skipEmptyCells);

            DrawDerivedCellSize();

            SyncNameListSize();

            EditorGUILayout.HelpBox(
                "Each row is one animation state, each column is one frame. " +
                "Cell size is derived from texture size, rows/cols, margin and spacing " +
                "(same as Unity's 'Grid By Cell Count' mode). " +
                "'Top To Bottom' means row 0 is the top row in the image. " +
                "'Skip Empty Cells' drops fully transparent cells (useful when rows have different frame counts).",
                MessageType.Info);
        }

        private void DrawDerivedCellSize() {
            if (settings.sourceTexture == null) {
                EditorGUILayout.LabelField("Cell Size (derived)", "-");
                return;
            }

            var cell = SpriteSheetSlicer.ComputeCellSize(settings);
            if (!cell.isValid) {
                EditorGUILayout.LabelField("Cell Size (derived)", "invalid - check margin/spacing");
                return;
            }

            var label = $"{cell.width} x {cell.height} px";
            if (cell.remainderX != 0 || cell.remainderY != 0) {
                label += $"  (leftover: {cell.remainderX}x{cell.remainderY})";
            }

            EditorGUILayout.LabelField("Cell Size (derived)", label);
        }

        private void DrawNamingSection() {
            EditorGUILayout.LabelField("3. Naming (row order)", EditorStyles.boldLabel);

            for (int i = 0; i < settings.stateNames.Count; i++) {
                settings.stateNames[i] = EditorGUILayout.TextField($"Row {i}", settings.stateNames[i]);
            }

            EditorGUILayout.HelpBox(
                "Enter a state name for each row. Frames will be named '<State>_00', '<State>_01', ...",
                MessageType.Info);
        }

        private void DrawPivotSection() {
            EditorGUILayout.LabelField("4. Pivot", EditorStyles.boldLabel);

            settings.pivotMode = (PivotMode)EditorGUILayout.EnumPopup("Pivot Mode", settings.pivotMode);

            using (new EditorGUI.DisabledScope(settings.pivotMode != PivotMode.CustomPixel)) {
                var px = EditorGUILayout.Vector2IntField("Pivot (px, from bottom-left)", settings.pivotPixels);
                settings.pivotPixels = px;
            }

            EditorGUILayout.HelpBox(
                "Pixel pivots are the usual choice for pixel-art characters (e.g. feet-center). " +
                "Unity stores pivots as normalized (0..1); this tool converts pixels per cell.",
                MessageType.Info);
        }

        private void DrawAnimationSection() {
            EditorGUILayout.LabelField("5. Animation", EditorStyles.boldLabel);

            settings.createClips = EditorGUILayout.Toggle("Create Clips", settings.createClips);

            using (new EditorGUI.DisabledScope(!settings.createClips)) {
                settings.clipFps = Mathf.Max(0.01f, EditorGUILayout.FloatField("Clip FPS", settings.clipFps));
                settings.loopClips = EditorGUILayout.Toggle("Loop Clips", settings.loopClips);
            }

            settings.createController = EditorGUILayout.Toggle("Create Animator Controller",
                settings.createController);

            using (new EditorGUI.DisabledScope(!settings.createController)) {
                settings.controllerName = EditorGUILayout.TextField("Controller Name", settings.controllerName);
            }

            EditorGUILayout.HelpBox(
                "Clips and controller are optional. Controller requires clip creation to be useful.",
                MessageType.Info);
        }

        private void DrawDraftTransitionsSection() {
            EditorGUILayout.LabelField("6. Draft Transitions", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!settings.createController)) {
                SyncTransitionsListSize();

                for (int i = 0; i < settings.transitionsTo.Count && i < settings.stateNames.Count; i++) {
                    var label = string.IsNullOrEmpty(settings.stateNames[i])
                        ? $"Row {i}"
                        : settings.stateNames[i];
                    settings.transitionsTo[i] = EditorGUILayout.TextField(label, settings.transitionsTo[i]);
                }
            }

            EditorGUILayout.HelpBox(
                "Enter target state names separated by commas. Example: 'Run, Jump, Attack'.\n" +
                "Unknown names produce warnings only. No conditions are added - review transitions manually.",
                MessageType.Info);
        }

        private void DrawActionsSection() {
            EditorGUILayout.LabelField("7. Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Preview / Validate")) {
                PreviewAndValidate();
            }

            using (new EditorGUI.DisabledScope(settings.sourceTexture == null)) {
                if (GUILayout.Button("Process")) {
                    TryProcess();
                }
            }

            if (GUILayout.Button("Clear", GUILayout.Width(80f))) {
                ClearSettings();
            }

            EditorGUILayout.EndHorizontal();

            DrawLastReport();
            DrawLastPreview();

            if (!string.IsNullOrEmpty(lastMessage)) {
                EditorGUILayout.HelpBox(lastMessage, lastMessageType);
            }
        }

        private void DrawLastReport() {
            if (lastReport == null) {
                return;
            }

            if (lastReport.HasErrors) {
                EditorGUILayout.HelpBox("Errors:\n- " + string.Join("\n- ", lastReport.errors), MessageType.Error);
            }

            if (lastReport.HasWarnings) {
                EditorGUILayout.HelpBox("Warnings:\n- " + string.Join("\n- ", lastReport.warnings),
                    MessageType.Warning);
            }
        }

        private void DrawLastPreview() {
            if (lastPreview == null) {
                return;
            }

            var msg = $"Sprites: {lastPreview.spriteCount}\n" +
                      $"Clips: {lastPreview.clipCount}\n" +
                      $"Controller: {(lastPreview.willCreateController ? "yes" : "no")}\n" +
                      $"Draft transitions: {lastPreview.draftTransitionCount}";

            if (lastPreview.unknownTransitionTargets.Count > 0) {
                msg += $"\nUnknown transition targets: {lastPreview.unknownTransitionTargets.Count}";
            }

            EditorGUILayout.HelpBox(msg, MessageType.None);
        }

        private void PreviewAndValidate() {
            lastReport = SpriteSheetImportValidator.Validate(settings);
            lastPreview = SpriteSheetImportProcessor.BuildPreviewSummary(settings);
            lastMessage = lastReport.HasErrors
                ? "Fix the errors above before running Process."
                : "Validation passed. You can run Process.";
            lastMessageType = lastReport.HasErrors ? MessageType.Error : MessageType.Info;
        }

        private void TryProcess() {
            lastReport = SpriteSheetImportValidator.Validate(settings);
            if (lastReport.HasErrors) {
                lastMessage = "Cannot process: validation failed. See errors above.";
                lastMessageType = MessageType.Error;
                return;
            }

            var outputFolder = SpriteSheetImportProcessor.ResolveOutputFolder(settings);
            var overwrites = SpriteSheetImportProcessor.CollectOverwriteTargets(settings, outputFolder);

            if (overwrites.Count > 0) {
                var sb = new StringBuilder();
                sb.AppendLine("Processing will overwrite:");
                sb.AppendLine();
                for (int i = 0; i < overwrites.Count; i++) {
                    sb.AppendLine("- " + overwrites[i]);
                }

                sb.AppendLine();
                sb.AppendLine("Continue?");

                if (!EditorUtility.DisplayDialog("Overwrite existing assets", sb.ToString(), "Overwrite", "Cancel")) {
                    lastMessage = "Processing cancelled by user.";
                    lastMessageType = MessageType.Warning;
                    return;
                }
            }

            var summary = SpriteSheetImportProcessor.Process(settings);
            lastPreview = summary;

            var resultMsg = new StringBuilder();
            resultMsg.Append($"Processed '{settings.sourceTexture.name}': ");
            resultMsg.Append($"{summary.spriteCount} sprite(s), ");
            resultMsg.Append($"{summary.clipCount} clip(s), ");
            resultMsg.Append($"{(summary.willCreateController ? 1 : 0)} controller, ");
            resultMsg.Append($"{summary.draftTransitionCount} transition(s).");

            if (summary.unknownTransitionTargets.Count > 0) {
                resultMsg.Append($"\nSkipped {summary.unknownTransitionTargets.Count} unknown transition target(s):");
                for (int i = 0; i < summary.unknownTransitionTargets.Count; i++) {
                    resultMsg.Append("\n- " + summary.unknownTransitionTargets[i]);
                }
            }

            lastMessage = resultMsg.ToString();
            lastMessageType = summary.unknownTransitionTargets.Count > 0 ? MessageType.Warning : MessageType.Info;

            Debug.Log("[SpriteSheetImporter] " + lastMessage);
        }

        private void ClearSettings() {
            settings = new SpriteSheetImportSettings();
            lastReport = null;
            lastPreview = null;
            lastMessage = null;
            lastMessageType = MessageType.Info;
            GUI.FocusControl(null);
        }

        private void AutoFillDefaultsForNewTexture() {
            if (settings.sourceTexture == null) {
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.controllerName)) {
                settings.controllerName = settings.sourceTexture.name;
            }
        }

        private void PickOutputFolder() {
            var defaultPath = string.IsNullOrWhiteSpace(settings.outputFolder)
                ? Application.dataPath
                : Path.Combine(Application.dataPath, settings.outputFolder.Substring("Assets/".Length));

            var picked = EditorUtility.OpenFolderPanel("Select Output Folder", defaultPath, "");
            if (string.IsNullOrEmpty(picked)) {
                return;
            }

            var dataPath = Application.dataPath.Replace('\\', '/');
            var pickedNormalized = picked.Replace('\\', '/');
            if (!pickedNormalized.StartsWith(dataPath)) {
                EditorUtility.DisplayDialog("Invalid folder", "Output folder must be inside the Assets folder.",
                    "OK");
                return;
            }

            settings.outputFolder = "Assets" + pickedNormalized.Substring(dataPath.Length);
        }

        /// <summary>
        /// Keeps the state names list in sync with the current row count without wiping existing entries.
        /// </summary>
        private void SyncNameListSize() {
            if (settings.stateNames == null) {
                settings.stateNames = new List<string>();
            }

            while (settings.stateNames.Count < settings.rows) {
                settings.stateNames.Add("");
            }

            while (settings.stateNames.Count > settings.rows) {
                settings.stateNames.RemoveAt(settings.stateNames.Count - 1);
            }
        }

        private void SyncTransitionsListSize() {
            if (settings.transitionsTo == null) {
                settings.transitionsTo = new List<string>();
            }

            while (settings.transitionsTo.Count < settings.rows) {
                settings.transitionsTo.Add("");
            }

            while (settings.transitionsTo.Count > settings.rows) {
                settings.transitionsTo.RemoveAt(settings.transitionsTo.Count - 1);
            }
        }
    }
}
