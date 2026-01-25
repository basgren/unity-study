using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Editor.Tools {
    /// <summary>
    /// Editor utility window that renames selected assets by replacing a substring in their file names.
    ///
    /// How it works:
    /// - Reads the current Project selection (Selection.assetGUIDs).
    /// - For each selected asset, replaces all occurrences of the specified substring in the asset's name
    ///   (file name without extension), then calls AssetDatabase.RenameAsset.
    /// - Uses AssetDatabase.RenameAsset so GUIDs stay the same and references remain intact.
    ///
    /// Limitations / notes:
    /// - This tool renames only top-level asset files addressed by paths in the AssetDatabase.
    /// - It does not rename sub-assets (e.g., individual sprites inside a spritesheet texture) because those
    ///   are not separate files on disk and require importer-level changes.
    /// - Optionally skips folders (recommended) to avoid renaming directory paths unexpectedly.
    /// - If a rename causes a name collision, Unity returns an error for that asset and it will be logged.
    /// </summary>
    public class AssetBatchRename : EditorWindow {
        private string from = "";
        private string to = "";
        private bool matchCase = true;
        private bool previewOnly = false;
        private bool skipFolders = true;

        [MenuItem("Tools/Rename/Replace Substring In Selected Assets...")]
        private static void Open() {
            GetWindow<AssetBatchRename>("Replace Substring");
        }

        private void OnGUI() {
            EditorGUILayout.LabelField("Replace substring in asset names (selected in Project).",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();

            from = EditorGUILayout.TextField("Find (substring)", from);
            to = EditorGUILayout.TextField("Replace with", to);

            matchCase = EditorGUILayout.Toggle("Match case", matchCase);
            previewOnly = EditorGUILayout.Toggle("Preview only (no changes)", previewOnly);
            skipFolders = EditorGUILayout.Toggle("Skip folders", skipFolders);

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(from))) {
                if (GUILayout.Button(previewOnly ? "Preview" : "Rename")) {
                    ReplaceInSelection(from, to, matchCase, previewOnly, skipFolders);
                }
            }

            EditorGUILayout.HelpBox(
                "Notes:\n" +
                "- Uses AssetDatabase.RenameAsset (keeps references).\n" +
                "- If 'Find' is empty, the button is disabled.\n" +
                "- If a rename causes a name collision, Unity will report an error for that asset.",
                MessageType.Info);
        }

        private static void ReplaceInSelection(string find, string replace, bool matchCase, bool previewOnly,
            bool skipFolders) {
            var guids = Selection.assetGUIDs;

            if (guids == null || guids.Length == 0) {
                Debug.LogWarning("No assets selected.");
                return;
            }

            var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var renamedCount = 0;

            AssetDatabase.StartAssetEditing();
            try {
                foreach (var guid in guids) {
                    var path = AssetDatabase.GUIDToAssetPath(guid);

                    if (string.IsNullOrEmpty(path)) {
                        continue;
                    }

                    if (skipFolders && AssetDatabase.IsValidFolder(path)) {
                        continue;
                    }

                    var fileName = Path.GetFileNameWithoutExtension(path);
                    var ext = Path.GetExtension(path);

                    if (fileName.IndexOf(find, comparison) < 0) {
                        continue;
                    }

                    var newFileName = ReplaceSubstring(fileName, find, replace, matchCase);

                    if (newFileName == fileName) {
                        continue;
                    }

                    if (previewOnly) {
                        Debug.Log($"[Preview] {path} -> {newFileName}{ext}");
                        continue;
                    }

                    // RenameAsset expects only the new name without extension.
                    var error = AssetDatabase.RenameAsset(path, newFileName);
                    if (!string.IsNullOrEmpty(error)) {
                        Debug.LogError($"Failed to rename '{path}' -> '{newFileName}{ext}': {error}");
                        continue;
                    }

                    renamedCount++;
                }
            }
            finally {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            if (previewOnly) {
                Debug.Log("Preview finished.");
            } else {
                Debug.Log($"Renamed assets: {renamedCount}");
            }
        }

        private static string ReplaceSubstring(string source, string find, string replace, bool matchCase) {
            if (matchCase) {
                return source.Replace(find, replace);
            }

            // Case-insensitive replacement for older Unity/.NET profile.
            var idx = source.IndexOf(find, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) {
                return source;
            }

            // Replace all occurrences case-insensitively.
            // We do it manually to avoid Regex overhead and escaping issues.
            var result = source;
            while (idx >= 0) {
                result = result.Substring(0, idx) + replace + result.Substring(idx + find.Length);
                idx = result.IndexOf(find, idx + replace.Length, StringComparison.OrdinalIgnoreCase);
            }

            return result;
        }
    }
}
