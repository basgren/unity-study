#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Game.Features.Portals.Common {
    /// <summary>
    /// Shared GUIStyle for portal scene-handle labels (Door, Entrance, ...).
    /// Lazily created and cached so OnDrawGizmos can call it cheaply every frame.
    /// </summary>
    public static class PortalGizmoLabelStyle {
        private static GUIStyle style;
        private static Texture2D bg;

        public static GUIStyle Get() {
            if (style != null) {
                return style;
            }

            bg = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            bg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.35f));
            bg.Apply();

            style = new GUIStyle(EditorStyles.boldLabel) {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                richText = false,
                wordWrap = false
            };

            style.normal.textColor = Color.white;
            style.normal.background = bg;
            style.padding = new RectOffset(8, 8, 5, 5);

            return style;
        }
    }
}
#endif
