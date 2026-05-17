using System;
using Game.Editor.SceneTopology.Model;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Editor.SceneTopology {
    /// <summary>
    /// Editor window that visualizes the scene-portal topology of all Build-Settings scenes.
    /// Read-only: a Refresh button re-scans every scene; cards show miniatures sized to the scene's
    /// CameraConfiner; entrance-to-entrance links drive approximate spatial alignment; cross-scene
    /// door links draw as dashed edges to nearby placed cards.
    /// </summary>
    public sealed class SceneTopologyWindow : EditorWindow {
        private SceneTopologyCanvas canvas;
        private Label statusLabel;
        private Label warningsLabel;
        private SceneTopologyGraph lastGraph;

        [MenuItem("Tools/Scene Topology")]
        public static void Open() {
            var window = GetWindow<SceneTopologyWindow>();
            window.titleContent = new GUIContent("Scene Topology");
            window.minSize = new Vector2(600f, 400f);
            window.Show();
        }

        private void CreateGUI() {
            var root = rootVisualElement;
            root.style.flexGrow = 1f;

            var toolbar = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    paddingTop = 4f, paddingBottom = 4f, paddingLeft = 6f, paddingRight = 6f,
                    backgroundColor = new Color(0.18f, 0.18f, 0.2f, 1f),
                    borderBottomWidth = 1f,
                    borderBottomColor = new Color(0f, 0f, 0f, 0.5f),
                    alignItems = Align.Center,
                },
            };

            var refreshBtn = new Button(Refresh) { text = "Refresh" };
            refreshBtn.style.marginRight = 6f;
            toolbar.Add(refreshBtn);

            var frameBtn = new Button(() => canvas?.ResetView()) { text = "Reset View" };
            frameBtn.style.marginRight = 12f;
            frameBtn.tooltip = "Reset zoom and pan (or press F over the canvas).";
            toolbar.Add(frameBtn);

            statusLabel = new Label("Click Refresh to scan scenes.") {
                style = {
                    color = new Color(0.85f, 0.85f, 0.85f, 1f),
                    flexGrow = 1f,
                },
            };
            toolbar.Add(statusLabel);

            root.Add(toolbar);

            canvas = new SceneTopologyCanvas();
            canvas.OnLinkCreated = Refresh;
            root.Add(canvas);

            var footer = new VisualElement {
                style = {
                    paddingTop = 2f, paddingBottom = 2f, paddingLeft = 6f, paddingRight = 6f,
                    backgroundColor = new Color(0.18f, 0.18f, 0.2f, 1f),
                    borderTopWidth = 1f,
                    borderTopColor = new Color(0f, 0f, 0f, 0.5f),
                },
            };
            warningsLabel = new Label(string.Empty) {
                style = {
                    color = new Color(1f, 0.8f, 0.4f, 1f),
                    fontSize = 10,
                    whiteSpace = WhiteSpace.NoWrap,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis,
                },
            };
            footer.Add(warningsLabel);
            root.Add(footer);
        }

        private void Refresh() {
            statusLabel.text = "Scanning...";
            warningsLabel.text = string.Empty;
            Repaint();

            SceneTopologyGraph graph = null;
            try {
                graph = SceneTopologyScanner.Scan();
            } catch (Exception ex) {
                EditorUtility.ClearProgressBar();
                Debug.LogException(ex);
                statusLabel.text = "Scan failed: " + ex.Message;
                return;
            }

            if (graph == null) {
                statusLabel.text = "Scan cancelled.";
                return;
            }

            lastGraph = graph;
            var layout = SceneTopologyLayout.Compute(graph);
            canvas.Populate(graph, layout);

            statusLabel.text = $"Scanned {graph.Nodes.Count} scenes " +
                $"({graph.CachedNodeCount} cached, {graph.FreshlyScannedNodeCount} rescanned), " +
                $"{graph.Edges.Count} portal links. Last refresh: {DateTime.Now:HH:mm:ss}";
            if (graph.Warnings.Count > 0) {
                warningsLabel.text = graph.Warnings.Count + " warning(s): " + string.Join(" | ", graph.Warnings);
                warningsLabel.tooltip = string.Join("\n", graph.Warnings);
            } else {
                warningsLabel.text = string.Empty;
                warningsLabel.tooltip = string.Empty;
            }
        }
    }
}
