using System.IO;
using CreatePlus.Core;
using CreatePlus.UI;
using UnityEditor;
using UnityEngine;

namespace CreatePlus {
    /// <summary>
    /// Entry points that open the Create Plus palette. These do not modify or replace Unity's built-in
    /// Create menu; they add an additional, separate command. Context (target folder, selection) is
    /// resolved here and handed to the UI-independent core.
    /// </summary>
    public static class CreatePlusMenuItems {
        // The default shortcut is Ctrl+Alt+N (Cmd+Alt+N on macOS). Ctrl+Shift+N was avoided because
        // Unity already binds it to "GameObject/Create Empty Child".
        const string OpenShortcut = " %&n";

        /// <summary>Opens Create Plus from the Project window context menu, for the selected folder.</summary>
        [MenuItem("Assets/Create Plus", priority = -100)]
        public static void OpenFromProject() {
            CreatePlusWindowIMGUI.Open(BuildProjectContext());
        }

        /// <summary>Opens Create Plus from the main menu (also carries the keyboard shortcut).</summary>
        [MenuItem("Tools/Create Plus/Open" + OpenShortcut)]
        public static void OpenFromTools() {
            CreatePlusWindowIMGUI.Open(BuildProjectContext());
        }

        /// <summary>Builds the execution context from the current Project window selection.</summary>
        static CreatePlusContext BuildProjectContext() {
            return new CreatePlusContext {
                OpenedFromProject = true,
                TargetFolderAssetPath = ResolveTargetFolder(),
                SelectedGameObject = Selection.activeGameObject,
                MousePosition = Vector2.zero
            };
        }

        /// <summary>
        /// Resolves the folder new assets should be created in:
        /// selected folder, the parent folder of a selected file, or "Assets" when nothing applies.
        /// </summary>
        static string ResolveTargetFolder() {
            Object selected = Selection.activeObject;
            if (selected == null) {
                return "Assets";
            }

            string path = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(path)) {
                return "Assets";
            }

            if (AssetDatabase.IsValidFolder(path)) {
                return path;
            }

            string parent = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(parent)) {
                return "Assets";
            }

            return parent.Replace('\\', '/');
        }
    }
}
