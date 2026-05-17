using Game.Editor.SceneTopology.Model;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Editor.SceneTopology {
    /// <summary>
    /// Visual representation of a single scanned scene: a tinted rounded box with the scene name
    /// and pins for every portal placed at proportional positions on its surface.
    /// Read-only — double-click opens the scene, right-click shows a context menu.
    /// </summary>
    public sealed class SceneCard : VisualElement {
        public SceneNodeData Data { get; }

        private const float NormalBorderWidth = 2f;
        private static readonly Color TitleColor = new Color(1f, 1f, 1f, 0.95f);
        private static readonly Color FallbackBorder = new Color(1f, 0.4f, 0.4f, 0.85f);
        private static readonly Color NormalBorder = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color HoverBorder = new Color(1f, 1f, 1f, 0.95f);

        private Color defaultBorderColor;

        public SceneCard(SceneNodeData data, Vector2 size) {
            Data = data;

            style.position = Position.Absolute;
            style.width = size.x;
            style.height = size.y;
            style.backgroundColor = ColorForRegion(data.RegionKey);

            // Border styling — red when bounds were faked (no CameraConfiner found).
            defaultBorderColor = data.BoundsAreFallback ? FallbackBorder : NormalBorder;
            style.borderTopWidth = NormalBorderWidth;
            style.borderBottomWidth = NormalBorderWidth;
            style.borderLeftWidth = NormalBorderWidth;
            style.borderRightWidth = NormalBorderWidth;
            SetBorderColor(defaultBorderColor);
            style.borderTopLeftRadius = 6f;
            style.borderTopRightRadius = 6f;
            style.borderBottomLeftRadius = 6f;
            style.borderBottomRightRadius = 6f;

            var title = new Label(data.DisplayName) {
                style = {
                    position = Position.Absolute,
                    left = 6f, top = 4f, right = 6f,
                    color = TitleColor,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 11,
                    unityTextAlign = TextAnchor.UpperLeft,
                    whiteSpace = WhiteSpace.NoWrap,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis,
                },
            };
            Add(title);

            if (data.BoundsAreFallback) {
                var tag = new Label("no confiner") {
                    style = {
                        position = Position.Absolute,
                        right = 6f, top = 4f,
                        color = FallbackBorder,
                        fontSize = 9,
                        unityFontStyleAndWeight = FontStyle.Italic,
                    },
                };
                Add(tag);
            }

            // Interactions: double-click opens, right-click shows menu.
            RegisterCallback<MouseDownEvent>(OnMouseDown);
        }

        /// <summary>Toggle hover styling (called by the canvas when this card is hovered).</summary>
        public void SetHighlighted(bool hovered) {
            SetBorderColor(hovered ? HoverBorder : defaultBorderColor);
        }

        private void SetBorderColor(Color color) {
            style.borderTopColor = color;
            style.borderBottomColor = color;
            style.borderLeftColor = color;
            style.borderRightColor = color;
        }

        private void OnMouseDown(MouseDownEvent evt) {
            if (evt.button == 0 && evt.clickCount >= 2) {
                OpenScene();
                evt.StopPropagation();
                return;
            }

            if (evt.button == 1) {
                ShowContextMenu();
                evt.StopPropagation();
            }
        }

        private void ShowContextMenu() {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Open Scene"), false, OpenScene);
            menu.AddItem(new GUIContent("Open Scene Additively"), false, OpenSceneAdditive);
            menu.AddItem(new GUIContent("Ping in Project"), false, PingInProject);
            menu.ShowAsContext();
        }

        private void OpenScene() {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                return;
            }

            EditorSceneManager.OpenScene(Data.ScenePath, OpenSceneMode.Single);
        }

        private void OpenSceneAdditive() {
            EditorSceneManager.OpenScene(Data.ScenePath, OpenSceneMode.Additive);
        }

        private void PingInProject() {
            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(Data.ScenePath);
            if (asset != null) {
                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
            }
        }

        private static Color ColorForRegion(string regionKey) {
            if (string.IsNullOrEmpty(regionKey)) {
                regionKey = "_default";
            }

            // Stable hash → hue. Saturation low and value medium so labels stay readable on top.
            var hash = unchecked((uint)StringHash(regionKey));
            var hue = (hash % 360u) / 360f;
            return Color.HSVToRGB(hue, 0.35f, 0.42f);
        }

        private static int StringHash(string s) {
            // FNV-1a — stable across .NET runtime versions (string.GetHashCode is randomized).
            unchecked {
                const int offset = (int)2166136261;
                const int prime = 16777619;
                var h = offset;
                for (var i = 0; i < s.Length; i++) {
                    h ^= s[i];
                    h *= prime;
                }

                return h;
            }
        }
    }
}
