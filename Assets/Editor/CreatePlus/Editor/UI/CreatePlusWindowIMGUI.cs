using CreatePlus.Core;
using UnityEditor;
using UnityEngine;

namespace CreatePlus.UI {
    /// <summary>
    /// IMGUI palette window for Create Plus. This is purely a view: it renders the model produced by
    /// <see cref="CreatePlusViewModel"/> and routes user actions to the core (registry, settings store,
    /// executor). It holds no business logic and stores no settings of its own (apart from transient
    /// view state like scroll position and the current search text).
    ///
    /// A future CreatePlusWindowUIToolkit can reuse the same core and view model without changes here.
    /// </summary>
    public sealed class CreatePlusWindowIMGUI : EditorWindow {
        const string SearchControlName = "CreatePlusSearch";
        const float ButtonSize = 18f;
        const float IndentStep = 14f;

        static CreatePlusWindowIMGUI instance;

        CreatePlusContext context;
        string searchQuery = string.Empty;
        bool showHidden;
        string selectedId;
        Vector2 leftScroll;
        Vector2 rightScroll;

        bool ignoreNextLostFocus;
        bool hasFocused;
        bool focusSearchPending;

        /// <summary>Opens (or re-opens) the palette for the given context.</summary>
        public static void Open(CreatePlusContext context) {
            if (instance != null) {
                instance.Close();
                instance = null;
            }

            var window = CreateInstance<CreatePlusWindowIMGUI>();
            window.context = context ?? CreatePlusContext.Empty;
            window.titleContent = new GUIContent("Create Plus");

            var size = new Vector2(760f, 520f);
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            Vector2 origin;
            if (context != null && context.MousePosition != Vector2.zero) {
                origin = context.MousePosition - new Vector2(size.x * 0.5f, 20f);
            } else {
                origin = main.center - size * 0.5f;
            }

            // Keep the palette fully inside the main editor window.
            origin.x = Mathf.Clamp(origin.x, main.xMin + 8f, main.xMax - size.x - 8f);
            origin.y = Mathf.Clamp(origin.y, main.yMin + 8f, main.yMax - size.y - 8f);

            window.position = new Rect(origin, size);
            window.ShowPopup();
            window.Focus();
            instance = window;
        }

        void OnEnable() {
            wantsMouseMove = true;
            focusSearchPending = true;
            CreatePlusSettingsStore.Changed += OnSettingsChanged;
        }

        void OnDisable() {
            CreatePlusSettingsStore.Changed -= OnSettingsChanged;
            if (instance == this) {
                instance = null;
            }
        }

        void OnFocus() {
            hasFocused = true;
        }

        void OnLostFocus() {
            // Close on outside click (palette behavior), but not when we intentionally opened a
            // context/settings menu, which transfers focus away momentarily.
            if (ignoreNextLostFocus) {
                ignoreNextLostFocus = false;
                return;
            }

            if (hasFocused) {
                Close();
            }
        }

        void OnSettingsChanged() {
            Repaint();
        }

        void OnGUI() {
            if (context == null) {
                context = CreatePlusContext.Empty;
            }

            CreatePlusStyles.EnsureBuilt();

            CreatePlusViewModel.Model model = CreatePlusViewModel.Build(searchQuery, showHidden);

            HandleKeyboard(model);

            DrawContextBar();
            DrawColumns(model);

            // Drive hover feedback.
            if (Event.current.type == EventType.MouseMove) {
                Repaint();
            }

            if (focusSearchPending && Event.current.type == EventType.Repaint) {
                EditorGUI.FocusTextInControl(SearchControlName);
                focusSearchPending = false;
            }
        }

        // ---- Top context bar -------------------------------------------------------------------

        void DrawContextBar() {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                GUILayout.Label("Target: " + context.DescribeTarget(), CreatePlusStyles.ContextBadge);
                GUILayout.FlexibleSpace();
                GUILayout.Label("Project Folder", EditorStyles.miniLabel);
                DrawBadge("Create Asset Here");
            }
        }

        void DrawBadge(string text) {
            GUILayout.Label(text, EditorStyles.miniBoldLabel);
        }

        // ---- Columns ---------------------------------------------------------------------------

        void DrawColumns(CreatePlusViewModel.Model model) {
            float leftWidth = Mathf.Max(220f, position.width * 0.38f);

            using (new EditorGUILayout.HorizontalScope()) {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(leftWidth))) {
                    leftScroll = EditorGUILayout.BeginScrollView(leftScroll);
                    DrawLeftColumn(model);
                    EditorGUILayout.EndScrollView();
                }

                DrawVerticalSeparator();

                using (new EditorGUILayout.VerticalScope()) {
                    rightScroll = EditorGUILayout.BeginScrollView(rightScroll);
                    DrawRightColumn(model);
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        void DrawLeftColumn(CreatePlusViewModel.Model model) {
            GUILayout.Label("Quick Access", CreatePlusStyles.PanelTitle);

            // Favorites (always available; stay visible while searching).
            if (DrawFoldout("Quick Access/Favorites", "Favorites", model.Favorites.Count, !model.FavoritesCollapsed, 0f)) {
                if (model.Favorites.Count == 0) {
                    DrawEmptyHint("No favorites yet. Use the star on any command.");
                } else {
                    foreach (CreatePlusCommand command in model.Favorites) {
                        DrawRow(command, 0f);
                    }
                }
            }

            // Recent (collapsed by default).
            if (DrawFoldout("Quick Access/Recent", "Recent", model.Recent.Count, !model.RecentCollapsed, 0f)) {
                if (model.Recent.Count == 0) {
                    DrawEmptyHint("No recent commands.");
                } else {
                    foreach (CreatePlusCommand command in model.Recent) {
                        DrawRow(command, 0f);
                    }
                }
            }

            DrawHorizontalSeparator();
            DrawSearchRow();
            DrawHorizontalSeparator();

            GUILayout.Label("Project", CreatePlusStyles.PanelTitle);
            foreach (CreatePlusViewModel.GroupNode group in model.Project.Groups) {
                DrawGroupNode(group);
            }
        }

        void DrawRightColumn(CreatePlusViewModel.Model model) {
            GUILayout.Label("Unity Common", CreatePlusStyles.PanelTitle);
            foreach (CreatePlusViewModel.GroupNode group in model.UnityCommon.Groups) {
                DrawGroupNode(group);
            }
        }

        void DrawSearchRow() {
            using (new EditorGUILayout.HorizontalScope()) {
                GUI.SetNextControlName(SearchControlName);
                searchQuery = EditorGUILayout.TextField(searchQuery, EditorStyles.toolbarSearchField);
                if (GUILayout.Button(CreatePlusIcons.Settings, CreatePlusStyles.IconButton, GUILayout.Width(22f), GUILayout.Height(18f))) {
                    ShowSettingsMenu();
                }
            }
        }

        // ---- Groups and rows -------------------------------------------------------------------

        bool IsSearching {
            get { return !string.IsNullOrWhiteSpace(searchQuery); }
        }

        void DrawGroupNode(CreatePlusViewModel.GroupNode node) {
            // During search, empty groups are hidden entirely so only matches remain.
            if (node.TotalCount == 0) {
                return;
            }

            float indent = node.Depth * IndentStep;
            bool expanded;
            if (IsSearching) {
                // Force-expand while filtering without mutating saved collapse state.
                DrawStaticHeader(node.Title, node.TotalCount, indent);
                expanded = true;
            } else {
                expanded = DrawFoldout(node.Key, node.Title, node.TotalCount, !node.Collapsed, indent);
            }

            if (expanded) {
                foreach (CreatePlusCommand command in node.Commands) {
                    DrawRow(command, indent + IndentStep);
                }

                foreach (CreatePlusViewModel.GroupNode child in node.SubGroups) {
                    DrawGroupNode(child);
                }
            } else {
                // Collapsed: keep pinned items (from the whole subtree) visible as compact rows.
                foreach (CreatePlusCommand command in node.Pinned) {
                    DrawPinnedMiniRow(command, indent + IndentStep);
                }
            }
        }

        bool DrawFoldout(string groupKey, string title, int count, bool currentlyExpanded, float indent) {
            Rect rect = GUILayoutUtility.GetRect(GUIContent.none, CreatePlusStyles.GroupHeader, GUILayout.ExpandWidth(true), GUILayout.Height(CreatePlusStyles.HeaderHeight));
            rect.xMin += indent;
            var label = new GUIContent(title + "  (" + count + ")");
            bool newExpanded = EditorGUI.Foldout(rect, currentlyExpanded, label, true, CreatePlusStyles.GroupHeader);
            if (newExpanded != currentlyExpanded) {
                CreatePlusSettingsStore.SetGroupCollapsed(groupKey, !newExpanded);
            }

            return newExpanded;
        }

        void DrawStaticHeader(string title, int count, float indent) {
            Rect rect = GUILayoutUtility.GetRect(GUIContent.none, CreatePlusStyles.GroupHeader, GUILayout.ExpandWidth(true), GUILayout.Height(CreatePlusStyles.HeaderHeight));
            rect.xMin += indent + 14f;
            EditorGUI.LabelField(rect, title + "  (" + count + ")", EditorStyles.boldLabel);
        }

        void DrawRow(CreatePlusCommand command, float indent) {
            Rect rect = GUILayoutUtility.GetRect(GUIContent.none, CreatePlusStyles.Row, GUILayout.ExpandWidth(true), GUILayout.Height(CreatePlusStyles.RowHeight));
            Event e = Event.current;
            bool hover = rect.Contains(e.mousePosition);
            bool selected = command.Id == selectedId;

            if (e.type == EventType.Repaint) {
                if (selected) {
                    EditorGUI.DrawRect(rect, CreatePlusStyles.SelectionColor);
                } else if (hover) {
                    EditorGUI.DrawRect(rect, CreatePlusStyles.HoverColor);
                }
            }

            bool isFavorite = CreatePlusSettingsStore.IsFavorite(command.Id);
            bool isPinned = CreatePlusSettingsStore.IsPinned(command.Id);

            // Right-aligned action buttons (more, pin, favorite from right to left).
            float slotMore = rect.xMax - 2f - ButtonSize;
            float slotPin = slotMore - ButtonSize;
            float slotFav = slotPin - ButtonSize;

            // Icon + label on the left, reserving room for the buttons.
            float x = rect.x + 4f + indent;
            Texture icon = CreatePlusIcons.GetCommandIcon(command);
            if (icon != null) {
                GUI.DrawTexture(new Rect(x, rect.y + 3f, 16f, 16f), icon, ScaleMode.ScaleToFit);
            }

            x += 20f;
            float labelRight = slotFav - 4f;
            var labelRect = new Rect(x, rect.y, Mathf.Max(20f, labelRight - x), rect.height);
            GUIStyle labelStyle = command.IsEnabled ? CreatePlusStyles.RowLabel : CreatePlusStyles.DimLabel;
            string tooltip = command.IsEnabled ? command.Tooltip : (command.DisabledReason ?? "Unavailable");
            GUI.Label(labelRect, new GUIContent(command.DisplayName, tooltip), labelStyle);

            // Execute on left-area click.
            if (e.type == EventType.MouseDown && e.button == 0 && labelRect.Contains(e.mousePosition)) {
                selectedId = command.Id;
                e.Use();
                ExecuteCommand(command);
                return;
            }

            // Favorite toggle (shown on hover or when active).
            if (hover || isFavorite) {
                var favRect = new Rect(slotFav, rect.y + 2f, ButtonSize, ButtonSize);
                if (GUI.Button(favRect, isFavorite ? CreatePlusIcons.FavoriteOn : CreatePlusIcons.FavoriteOff, CreatePlusStyles.IconButton)) {
                    CreatePlusSettingsStore.ToggleFavorite(command.Id);
                }
            }

            // Pin toggle.
            if (hover || isPinned) {
                var pinRect = new Rect(slotPin, rect.y + 2f, ButtonSize, ButtonSize);
                if (GUI.Button(pinRect, isPinned ? CreatePlusIcons.PinOn : CreatePlusIcons.PinOff, CreatePlusStyles.IconButton)) {
                    CreatePlusSettingsStore.TogglePinned(command.Id);
                }
            }

            // More menu.
            if (hover) {
                var moreRect = new Rect(slotMore, rect.y + 2f, ButtonSize, ButtonSize);
                if (GUI.Button(moreRect, CreatePlusIcons.More, CreatePlusStyles.IconButton)) {
                    ShowMoreMenu(command);
                }
            }
        }

        void DrawPinnedMiniRow(CreatePlusCommand command, float indent) {
            Rect rect = GUILayoutUtility.GetRect(GUIContent.none, CreatePlusStyles.PinnedMiniRow, GUILayout.ExpandWidth(true), GUILayout.Height(18f));
            Event e = Event.current;
            bool hover = rect.Contains(e.mousePosition);
            if (e.type == EventType.Repaint && hover) {
                EditorGUI.DrawRect(rect, CreatePlusStyles.HoverColor);
            }

            Rect labelRect = rect;
            labelRect.xMin += indent;
            GUI.Label(labelRect, new GUIContent(command.DisplayName, command.Tooltip), CreatePlusStyles.PinnedMiniRow);
            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition)) {
                selectedId = command.Id;
                e.Use();
                ExecuteCommand(command);
            }
        }

        // ---- Menus -----------------------------------------------------------------------------

        void ShowMoreMenu(CreatePlusCommand command) {
            var menu = new GenericMenu();
            bool isFavorite = CreatePlusSettingsStore.IsFavorite(command.Id);
            bool isPinned = CreatePlusSettingsStore.IsPinned(command.Id);
            bool isHidden = CreatePlusSettingsStore.IsHidden(command.Id);

            menu.AddItem(new GUIContent(isFavorite ? "Remove from Favorites" : "Add to Favorites"), false,
                () => CreatePlusSettingsStore.ToggleFavorite(command.Id));
            menu.AddItem(new GUIContent(isPinned ? "Unpin" : "Pin in Group"), false,
                () => CreatePlusSettingsStore.TogglePinned(command.Id));
            menu.AddItem(new GUIContent(isHidden ? "Unhide" : "Hide from Create Plus"), false,
                () => CreatePlusSettingsStore.ToggleHidden(command.Id));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Show Original Unity Path"), false,
                () => Debug.Log("[Create Plus] " + command.DisplayName + "  ->  " + command.OriginalPath));
            menu.AddItem(new GUIContent("Reset Command Settings"), false,
                () => CreatePlusSettingsStore.ResetCommand(command.Id));

            ignoreNextLostFocus = true;
            menu.ShowAsContext();
        }

        void ShowSettingsMenu() {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Show Hidden Commands"), showHidden, () => {
                showHidden = !showHidden;
                Repaint();
            });
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Reset All Create Plus Settings"), false, () => {
                if (EditorUtility.DisplayDialog("Create Plus",
                        "Reset all Create Plus settings (favorites, pins, hidden, recent, usage)?",
                        "Reset", "Cancel")) {
                    CreatePlusSettingsStore.ResetAll();
                }
            });

            ignoreNextLostFocus = true;
            menu.ShowAsContext();
        }

        // ---- Keyboard --------------------------------------------------------------------------

        void HandleKeyboard(CreatePlusViewModel.Model model) {
            Event e = Event.current;
            if (e.type != EventType.KeyDown) {
                return;
            }

            switch (e.keyCode) {
                case KeyCode.Escape:
                    e.Use();
                    Close();
                    GUIUtility.ExitGUI();
                    break;
                case KeyCode.DownArrow:
                    MoveSelection(model, 1);
                    e.Use();
                    break;
                case KeyCode.UpArrow:
                    MoveSelection(model, -1);
                    e.Use();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    CreatePlusCommand command = ResolveSelected(model);
                    if (command != null) {
                        e.Use();
                        ExecuteCommand(command);
                    }

                    break;
            }
        }

        void MoveSelection(CreatePlusViewModel.Model model, int direction) {
            System.Collections.Generic.List<CreatePlusCommand> nav = model.NavOrder;
            if (nav.Count == 0) {
                return;
            }

            int index = nav.FindIndex(c => c.Id == selectedId);
            if (index < 0) {
                index = direction > 0 ? 0 : nav.Count - 1;
            } else {
                index = Mathf.Clamp(index + direction, 0, nav.Count - 1);
            }

            selectedId = nav[index].Id;
            Repaint();
        }

        CreatePlusCommand ResolveSelected(CreatePlusViewModel.Model model) {
            System.Collections.Generic.List<CreatePlusCommand> nav = model.NavOrder;
            if (nav.Count == 0) {
                return null;
            }

            int index = nav.FindIndex(c => c.Id == selectedId);
            return index >= 0 ? nav[index] : nav[0];
        }

        // ---- Execution -------------------------------------------------------------------------

        void ExecuteCommand(CreatePlusCommand command) {
            bool success = CreatePlusCommandExecutor.Execute(command, context);
            if (success) {
                Close();
                GUIUtility.ExitGUI();
            } else {
                Repaint();
            }
        }

        // ---- Small drawing helpers -------------------------------------------------------------

        void DrawEmptyHint(string text) {
            GUILayout.Label(text, EditorStyles.miniLabel);
        }

        void DrawHorizontalSeparator() {
            Rect rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            rect.height = 1f;
            rect.y += 2f;
            EditorGUI.DrawRect(rect, CreatePlusStyles.SeparatorColor);
            GUILayout.Space(2f);
        }

        void DrawVerticalSeparator() {
            Rect rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            rect.width = 1f;
            EditorGUI.DrawRect(rect, CreatePlusStyles.SeparatorColor);
        }
    }
}
