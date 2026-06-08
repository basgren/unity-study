using UnityEditor;
using UnityEngine;

namespace CreatePlus.UI {
    /// <summary>
    /// Lazily-created GUI styles for the IMGUI palette. GUIStyle instances must be created inside the
    /// OnGUI call stack, so styles are built on first use via <see cref="EnsureBuilt"/>. Kept separate
    /// from the window so layout code stays readable.
    /// </summary>
    public static class CreatePlusStyles {
        public static GUIStyle PanelTitle { get; private set; }
        public static GUIStyle GroupHeader { get; private set; }
        public static GUIStyle Row { get; private set; }
        public static GUIStyle RowSelected { get; private set; }
        public static GUIStyle RowLabel { get; private set; }
        public static GUIStyle PinnedMiniRow { get; private set; }
        public static GUIStyle IconButton { get; private set; }
        public static GUIStyle ContextBadge { get; private set; }
        public static GUIStyle DimLabel { get; private set; }

        static bool built;

        /// <summary>The compact height of a command row.</summary>
        public const float RowHeight = 22f;

        /// <summary>The height of a group header.</summary>
        public const float HeaderHeight = 24f;

        public static void EnsureBuilt() {
            if (built) {
                return;
            }

            PanelTitle = new GUIStyle(EditorStyles.boldLabel) {
                fontSize = 12,
                margin = new RectOffset(4, 4, 6, 2)
            };

            GroupHeader = new GUIStyle(EditorStyles.foldout) {
                fontStyle = FontStyle.Bold,
                fixedHeight = HeaderHeight
            };

            RowLabel = new GUIStyle(EditorStyles.label) {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(2, 2, 0, 0)
            };

            Row = new GUIStyle(GUIStyle.none) {
                fixedHeight = RowHeight
            };

            RowSelected = new GUIStyle(Row);

            PinnedMiniRow = new GUIStyle(EditorStyles.miniLabel) {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(18, 2, 0, 0)
            };

            IconButton = new GUIStyle(EditorStyles.label) {
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0),
                fontSize = 12
            };

            ContextBadge = new GUIStyle(EditorStyles.miniLabel) {
                alignment = TextAnchor.MiddleLeft
            };

            DimLabel = new GUIStyle(EditorStyles.label) {
                alignment = TextAnchor.MiddleLeft
            };
            Color dim = DimLabel.normal.textColor;
            dim.a = 0.5f;
            DimLabel.normal.textColor = dim;

            built = true;
        }

        /// <summary>Background tint used to highlight the keyboard-selected row.</summary>
        public static Color SelectionColor {
            get {
                return EditorGUIUtility.isProSkin
                    ? new Color(0.24f, 0.37f, 0.59f, 0.6f)
                    : new Color(0.36f, 0.55f, 0.85f, 0.5f);
            }
        }

        /// <summary>Subtle hover tint for rows.</summary>
        public static Color HoverColor {
            get {
                return EditorGUIUtility.isProSkin
                    ? new Color(1f, 1f, 1f, 0.06f)
                    : new Color(0f, 0f, 0f, 0.06f);
            }
        }

        /// <summary>Color of the thin separators between sections.</summary>
        public static Color SeparatorColor {
            get {
                return EditorGUIUtility.isProSkin
                    ? new Color(0f, 0f, 0f, 0.4f)
                    : new Color(0f, 0f, 0f, 0.2f);
            }
        }
    }
}
