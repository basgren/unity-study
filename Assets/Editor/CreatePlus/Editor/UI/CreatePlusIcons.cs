using CreatePlus.Core;
using UnityEditor;
using UnityEngine;

namespace CreatePlus.UI {
    /// <summary>
    /// Resolves icons and action glyphs for the palette. Prefers Unity's built-in editor icons and
    /// falls back to text glyphs when an icon is unavailable, so the UI degrades gracefully across
    /// editor versions. Tooltips are always provided for the icon-only buttons.
    /// </summary>
    public static class CreatePlusIcons {
        static GUIContent favoriteOn;
        static GUIContent favoriteOff;
        static GUIContent pinOn;
        static GUIContent pinOff;
        static GUIContent more;
        static GUIContent settings;
        static bool built;

        public static GUIContent FavoriteOn { get { EnsureBuilt(); return favoriteOn; } }
        public static GUIContent FavoriteOff { get { EnsureBuilt(); return favoriteOff; } }
        public static GUIContent PinOn { get { EnsureBuilt(); return pinOn; } }
        public static GUIContent PinOff { get { EnsureBuilt(); return pinOff; } }
        public static GUIContent More { get { EnsureBuilt(); return more; } }
        public static GUIContent Settings { get { EnsureBuilt(); return settings; } }

        static void EnsureBuilt() {
            if (built) {
                return;
            }

            favoriteOn = Resolve("Remove from Favorites", "★", "Favorite", "d_Favorite");
            favoriteOff = Resolve("Add to Favorites", "☆");
            pinOn = Resolve("Unpin", "■", "pin", "d_pin", "PinIcon");
            pinOff = Resolve("Pin in Group", "□");
            more = Resolve("More", "⋮", "_Menu", "d__Menu");
            settings = Resolve("Settings", "⚙", "_Popup", "d__Popup", "SettingsIcon");

            built = true;
        }

        /// <summary>Returns the command's left-hand icon based on its kind/id, or null.</summary>
        public static Texture GetCommandIcon(CreatePlusCommand command) {
            if (command == null) {
                return null;
            }

            string iconName = null;
            switch (command.Id) {
                case "builtin.asset.folder":
                    iconName = "Folder Icon";
                    break;
                case "builtin.asset.csharp-script":
                    iconName = "cs Script Icon";
                    break;
                case "builtin.asset.material":
                case "builtin.asset.material-variant":
                    iconName = "Material Icon";
                    break;
                case "builtin.asset.scene":
                    iconName = "SceneAsset Icon";
                    break;
                case "builtin.asset.text":
                    iconName = "TextAsset Icon";
                    break;
                case "builtin.shader":
                case "builtin.shadergraph":
                    iconName = "Shader Icon";
                    break;
            }

            if (iconName != null) {
                GUIContent content = EditorGUIUtility.IconContent(iconName);
                if (content != null && content.image != null) {
                    return content.image;
                }
            }

            // Default generic asset icon.
            GUIContent fallback = EditorGUIUtility.IconContent("DefaultAsset Icon");
            return fallback != null ? fallback.image : null;
        }

        static GUIContent Resolve(string tooltip, string fallbackText, params string[] iconNames) {
            if (iconNames != null) {
                foreach (string name in iconNames) {
                    GUIContent content = EditorGUIUtility.IconContent(name);
                    if (content != null && content.image != null) {
                        return new GUIContent(content.image, tooltip);
                    }
                }
            }

            return new GUIContent(fallbackText, tooltip);
        }
    }
}
