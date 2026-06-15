#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.Editor.SceneTools {
    /// <summary>
    /// Unified editor window to run per-scene maintenance operations (validate / fix) over one or
    /// more scenes. Operations implementing <see cref="ISceneOperation"/> are discovered
    /// automatically. Scenes are shown as a collapsible checkbox tree; ticking a directory selects
    /// every scene under it. See docs/system/scene-tools-window.md.
    /// </summary>
    public sealed class SceneToolsWindow : EditorWindow {
        private const float IndentWidth = 14f;
        private const float TwirlWidth = 13f;

        private readonly List<ISceneOperation> operations = new List<ISceneOperation>();
        private readonly HashSet<string> selectedScenePaths = new HashSet<string>();
        private readonly HashSet<string> collapsedDirs = new HashSet<string>();
        private readonly List<SceneNode> allNodes = new List<SceneNode>();
        private List<string> scenePaths = new List<string>();

        private ISceneOperation currentOperation;
        private SceneSource sceneSource = SceneSource.BuildSettings;

        private ScrollView sceneListView;
        private Label statusLabel;
        private Button runButton;

        [MenuItem("Tools/Scene Tools")]
        public static void Open() {
            var window = GetWindow<SceneToolsWindow>();
            window.titleContent = new GUIContent("Scene Tools");
            window.minSize = new Vector2(420f, 360f);
            window.Show();
        }

        private void CreateGUI() {
            DiscoverOperations();

            var root = rootVisualElement;
            root.style.flexGrow = 1f;

            BuildToolbar(root);
            BuildSelectionBar(root);

            // flexBasis = 0 so the list takes only the leftover space and scrolls internally; without
            // it the ScrollView sizes to its content and pushes the footer off-screen.
            sceneListView = new ScrollView {
                style = { flexGrow = 1f, flexShrink = 1f, flexBasis = 0f, minHeight = 0f, paddingLeft = 4f },
            };
            root.Add(sceneListView);

            BuildFooter(root);

            RefreshScenes();
            UpdateRunButton();
        }

        private void DiscoverOperations() {
            operations.Clear();
            foreach (var type in TypeCache.GetTypesDerivedFrom<ISceneOperation>()) {
                if (type.IsAbstract || type.IsInterface || type.GetConstructor(Type.EmptyTypes) == null) {
                    continue;
                }

                operations.Add((ISceneOperation)Activator.CreateInstance(type));
            }

            operations.Sort((a, b) => {
                var byCategory = string.Compare(a.Category, b.Category, StringComparison.Ordinal);
                return byCategory != 0
                    ? byCategory
                    : string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal);
            });

            currentOperation = operations.Count > 0 ? operations[0] : null;
        }

        private void BuildToolbar(VisualElement root) {
            var toolbar = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexShrink = 0f,
                    paddingTop = 4f, paddingBottom = 4f, paddingLeft = 6f, paddingRight = 6f,
                    backgroundColor = new Color(0.18f, 0.18f, 0.2f, 1f),
                    alignItems = Align.Center,
                },
            };

            if (operations.Count > 0) {
                var opChoices = operations.Select(OperationLabel).ToList();
                var opField = new PopupField<string>("Operation", opChoices, 0) {
                    style = { flexGrow = 1f, marginRight = 8f },
                };
                opField.RegisterValueChangedCallback(evt => {
                    var index = opChoices.IndexOf(evt.newValue);
                    currentOperation = index >= 0 ? operations[index] : null;
                    UpdateRunButton();
                });
                toolbar.Add(opField);
            } else {
                toolbar.Add(new Label("No scene operations found."));
            }

            var sources = new List<SceneSource> { SceneSource.BuildSettings, SceneSource.AllProject };
            var sourceField = new PopupField<SceneSource>("Scenes", sources, 0, SourceLabel, SourceLabel) {
                style = { marginRight = 8f },
            };
            sourceField.RegisterValueChangedCallback(evt => {
                sceneSource = evt.newValue;
                RefreshScenes();
            });
            toolbar.Add(sourceField);

            toolbar.Add(new Button(RefreshScenes) { text = "Refresh" });

            root.Add(toolbar);
        }

        private void BuildSelectionBar(VisualElement root) {
            var selectionBar = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    flexShrink = 0f,
                    paddingTop = 4f, paddingBottom = 4f, paddingLeft = 6f, paddingRight = 6f,
                    alignItems = Align.Center,
                },
            };
            selectionBar.Add(new Button(SelectAll) { text = "All" });
            selectionBar.Add(new Button(SelectNone) { text = "None" });
            selectionBar.Add(new Button(SelectOpen) { text = "Open" });
            root.Add(selectionBar);
        }

        private void BuildFooter(VisualElement root) {
            var footer = new VisualElement {
                style = {
                    flexShrink = 0f,
                    paddingTop = 4f, paddingBottom = 6f, paddingLeft = 6f, paddingRight = 6f,
                    borderTopWidth = 1f, borderTopColor = new Color(0f, 0f, 0f, 0.4f),
                },
            };

            statusLabel = new Label(string.Empty) {
                style = {
                    color = new Color(0.8f, 0.8f, 0.8f, 1f),
                    marginBottom = 4f,
                    whiteSpace = WhiteSpace.Normal,
                },
            };
            footer.Add(statusLabel);

            runButton = new Button(RunSelected) { text = "Run" };
            footer.Add(runButton);

            root.Add(footer);
        }

        private static string OperationLabel(ISceneOperation op) {
            return string.IsNullOrEmpty(op.Category) ? op.DisplayName : $"{op.Category} / {op.DisplayName}";
        }

        private static string SourceLabel(SceneSource source) {
            return source == SceneSource.BuildSettings ? "Build Settings" : "All Project";
        }

        private void RefreshScenes() {
            scenePaths = sceneSource == SceneSource.BuildSettings
                ? GetBuildSettingsScenes()
                : GetAllProjectScenes();

            // Drop selections that no longer exist; default to the currently-open scenes when empty.
            selectedScenePaths.RemoveWhere(p => !scenePaths.Contains(p));
            if (selectedScenePaths.Count == 0) {
                foreach (var path in GetOpenScenePaths()) {
                    if (scenePaths.Contains(path)) {
                        selectedScenePaths.Add(path);
                    }
                }
            }

            RebuildSceneTree();
            UpdateRunButton();
        }

        private void RebuildSceneTree() {
            sceneListView.Clear();
            allNodes.Clear();

            if (scenePaths.Count == 0) {
                sceneListView.Add(new Label("No scenes found for this source."));
                return;
            }

            var root = BuildTree(scenePaths);
            RenderChildren(root, sceneListView, 0);
            RefreshToggleStates();
        }

        private void RenderChildren(SceneNode node, VisualElement container, int depth) {
            var ordered = node.Children
                .OrderBy(c => c.IsScene ? 1 : 0)
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var child in ordered) {
                allNodes.Add(child);

                var row = new VisualElement {
                    style = {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        marginLeft = depth * IndentWidth,
                    },
                };

                VisualElement childrenContainer = null;

                if (child.IsScene) {
                    var spacer = new VisualElement { style = { width = TwirlWidth, flexShrink = 0f } };
                    row.Add(spacer);
                } else {
                    var expanded = !collapsedDirs.Contains(child.DirectoryPath);
                    // Borderless small triangle (U+25BE / U+25B8) to match the standard foldout arrow.
                    var twirl = new Label(expanded ? "▾" : "▸") {
                        style = {
                            width = TwirlWidth,
                            flexShrink = 0f,
                            unityTextAlign = TextAnchor.MiddleCenter,
                            fontSize = 16f,
                            color = new Color(0.75f, 0.75f, 0.75f, 1f),
                        },
                    };
                    row.Add(twirl);

                    childrenContainer = new VisualElement();
                    childrenContainer.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;

                    var capturedDir = child.DirectoryPath;
                    twirl.RegisterCallback<ClickEvent>(_ => {
                        var nowExpanded = collapsedDirs.Contains(capturedDir);
                        if (nowExpanded) {
                            collapsedDirs.Remove(capturedDir);
                        } else {
                            collapsedDirs.Add(capturedDir);
                        }

                        twirl.text = nowExpanded ? "▾" : "▸";
                        childrenContainer.style.display = nowExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                    });
                }

                var labelText = child.IsScene ? SceneLabel(child.Name) : child.Name;
                var toggle = new Toggle { style = { marginRight = 4f } };
                child.Toggle = toggle;
                toggle.RegisterValueChangedCallback(evt => OnNodeToggled(child, evt.newValue));
                row.Add(toggle);

                // Clicking the name flips its checkbox (value setter notifies, so OnNodeToggled runs).
                var nameLabel = new Label(labelText) { style = { flexGrow = 1f } };
                nameLabel.RegisterCallback<ClickEvent>(_ => toggle.value = !toggle.value);
                row.Add(nameLabel);

                container.Add(row);

                if (childrenContainer != null) {
                    container.Add(childrenContainer);
                    RenderChildren(child, childrenContainer, depth + 1);
                }
            }
        }

        private void OnNodeToggled(SceneNode node, bool selected) {
            foreach (var path in node.DescendantScenePaths) {
                if (selected) {
                    selectedScenePaths.Add(path);
                } else {
                    selectedScenePaths.Remove(path);
                }
            }

            RefreshToggleStates();
            UpdateRunButton();
        }

        /// <summary>Syncs every toggle to the selection without firing change callbacks.</summary>
        private void RefreshToggleStates() {
            foreach (var node in allNodes) {
                if (node.Toggle == null) {
                    continue;
                }

                bool value = node.IsScene
                    ? selectedScenePaths.Contains(node.ScenePath)
                    : node.DescendantScenePaths.Count > 0 && node.DescendantScenePaths.All(selectedScenePaths.Contains);

                node.Toggle.SetValueWithoutNotify(value);
            }
        }

        private void SelectAll() {
            selectedScenePaths.Clear();
            foreach (var path in scenePaths) {
                selectedScenePaths.Add(path);
            }

            RefreshToggleStates();
            UpdateRunButton();
        }

        private void SelectNone() {
            selectedScenePaths.Clear();
            RefreshToggleStates();
            UpdateRunButton();
        }

        private void SelectOpen() {
            selectedScenePaths.Clear();
            foreach (var path in GetOpenScenePaths()) {
                if (scenePaths.Contains(path)) {
                    selectedScenePaths.Add(path);
                }
            }

            RefreshToggleStates();
            UpdateRunButton();
        }

        private void RunSelected() {
            if (currentOperation == null) {
                return;
            }

            var paths = scenePaths.Where(selectedScenePaths.Contains).ToList();
            SceneOperationRunner.Run(currentOperation, paths);

            // The runner may have opened/closed scenes; refresh so the toggles match reality.
            RefreshScenes();
            statusLabel.text =
                $"Last run: {currentOperation.DisplayName} on {paths.Count} scene(s). See Console for details.";
        }

        private void UpdateRunButton() {
            if (runButton == null) {
                return;
            }

            var count = scenePaths.Count(selectedScenePaths.Contains);
            var opName = currentOperation != null ? currentOperation.DisplayName : "—";
            runButton.text = $"Run \"{opName}\" on {count} scene(s)";
            runButton.SetEnabled(currentOperation != null && count > 0);

            statusLabel.text = currentOperation != null && currentOperation.Mutates
                ? "This operation may modify and save matching scenes."
                : "Read-only validation. No scenes will be modified.";
        }

        private static string SceneLabel(string fileName) {
            return fileName.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                ? fileName.Substring(0, fileName.Length - ".unity".Length)
                : fileName;
        }

        // --- tree model ---------------------------------------------------------------------

        private sealed class SceneNode {
            public string Name;
            public string DirectoryPath;   // set for directory nodes
            public string ScenePath;       // set for scene leaves
            public bool IsScene => ScenePath != null;
            public readonly List<SceneNode> Children = new List<SceneNode>();
            public readonly List<string> DescendantScenePaths = new List<string>();
            public Toggle Toggle;
        }

        private static SceneNode BuildTree(IEnumerable<string> paths) {
            var root = new SceneNode { Name = string.Empty, DirectoryPath = string.Empty };

            foreach (var path in paths) {
                var segments = path.Split('/');
                var node = root;
                var accum = string.Empty;

                for (var i = 0; i < segments.Length; i++) {
                    var segment = segments[i];
                    accum = i == 0 ? segment : accum + "/" + segment;

                    if (i == segments.Length - 1) {
                        node.Children.Add(new SceneNode { Name = segment, ScenePath = path });
                    } else {
                        var child = node.Children.Find(c => !c.IsScene && c.Name == segment);
                        if (child == null) {
                            child = new SceneNode { Name = segment, DirectoryPath = accum };
                            node.Children.Add(child);
                        }

                        node = child;
                    }
                }
            }

            for (var i = 0; i < root.Children.Count; i++) {
                root.Children[i] = Compress(root.Children[i]);
            }

            ComputeDescendants(root);
            return root;
        }

        // Collapses chains of single-child directories (e.g. Assets/Game/Scenes) into one row.
        private static SceneNode Compress(SceneNode node) {
            for (var i = 0; i < node.Children.Count; i++) {
                node.Children[i] = Compress(node.Children[i]);
            }

            if (!node.IsScene && node.Children.Count == 1 && !node.Children[0].IsScene) {
                var only = node.Children[0];
                var merged = new SceneNode {
                    Name = node.Name + "/" + only.Name,
                    DirectoryPath = only.DirectoryPath,
                };
                merged.Children.AddRange(only.Children);
                return merged;
            }

            return node;
        }

        private static void ComputeDescendants(SceneNode node) {
            if (node.IsScene) {
                node.DescendantScenePaths.Add(node.ScenePath);
                return;
            }

            foreach (var child in node.Children) {
                ComputeDescendants(child);
                node.DescendantScenePaths.AddRange(child.DescendantScenePaths);
            }
        }

        // --- scene sources ------------------------------------------------------------------

        private static List<string> GetBuildSettingsScenes() {
            var result = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes) {
                if (!string.IsNullOrEmpty(scene.path)) {
                    result.Add(scene.path);
                }
            }

            return result;
        }

        private static List<string> GetAllProjectScenes() {
            var result = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Scene")) {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/", StringComparison.Ordinal)) {
                    result.Add(path);
                }
            }

            result.Sort(StringComparer.Ordinal);
            return result;
        }

        private static List<string> GetOpenScenePaths() {
            var result = new List<string>();
            for (var i = 0; i < SceneManager.sceneCount; i++) {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && !string.IsNullOrEmpty(scene.path)) {
                    result.Add(scene.path);
                }
            }

            return result;
        }
    }
}
#endif
