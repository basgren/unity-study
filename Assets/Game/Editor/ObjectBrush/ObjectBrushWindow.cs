using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Editor.ObjectBrush {
    /// <summary>
    /// Editor window that provides an object painting brush for the Scene view.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Object Brush lets you quickly place prefab instances into the Scene view,
    /// using a palette similar to a tile palette but for arbitrary GameObjects.
    /// Open it from the main menu: <c>Tools &gt; Object Brush</c>.
    /// </para>
    ///
    /// <para><b>Palette structure</b></para>
    /// <para>
    /// The palette is organised as <b>Biome → Category → Items</b>. Biomes are
    /// <see cref="ObjectBrushProfile"/> assets referenced by the shared
    /// <see cref="ObjectBrushConfig"/>; several biomes are shown at once. Categories and
    /// their parent paths, the World root name, and the referenced biome list are edited in
    /// the separate <see cref="ObjectBrushConfigWindow"/> (<c>Tools &gt; Object Brush
    /// Configuration</c>, or the <c>Configure…</c> button). This window assigns prefab slots
    /// and paints.
    /// </para>
    ///
    /// <para><b>Parenting</b></para>
    /// <para>
    /// When an object is placed, it is nested under
    /// <c>&lt;World root&gt;/&lt;category parent path&gt;</c> in the active scene, creating any
    /// missing objects on the way. The mapping is the same for every scene; there are no
    /// per-scene parent settings.
    /// </para>
    ///
    /// <para><b>Grid snapping</b></para>
    /// <para>
    /// When <c>Snap to Grid</c> is enabled, placed objects snap to a regular grid with the
    /// configured <c>Grid Size</c>. Holding <c>Ctrl</c> temporarily inverts snapping.
    /// </para>
    ///
    /// <para><b>Hotkeys</b></para>
    /// <para>
    /// While the Scene view is focused: <c>\</c> toggles painting on/off, and <c>[</c> / <c>]</c>
    /// select the previous / next palette item, cycling across every item in all biomes.
    /// </para>
    /// </remarks>
    public class ObjectBrushWindow : EditorWindow {
        [SerializeField]
        private GameObject prefab;

        [SerializeField]
        private string activeParentPath;

        [SerializeField]
        private bool snapToGrid = true;

        [SerializeField]
        private float gridSize = 1f;

        [SerializeField]
        private float viewIconSize = 40f;

        [SerializeField]
        private bool brushEnabled;

        [SerializeField]
        private string filterText = "";

        [SerializeField]
        private Vector2 categoriesScroll;

        [SerializeField]
        private bool brushFoldout = true;

        // When enabled, the palette shows editable per-category item lists (add/remove/assign).
        // When disabled (default), it shows a compact, read-only wrapping grid of previews for picking.
        [SerializeField]
        private bool editMode;

        private ObjectBrushConfig config;

        // Highlight state for the active slot. Best-effort, reset on domain reload.
        [NonSerialized]
        private ObjectBrushProfile.BiomeCategory activeCategory;

        [NonSerialized]
        private int activeItemIndex = -1;

        // When set, the palette scrolls to bring the active slot into view on the next repaint.
        [NonSerialized]
        private bool scrollToActive;

        [NonSerialized]
        private bool hasPendingActiveRect;

        [NonSerialized]
        private Rect pendingActiveRect;

        private readonly Dictionary<ObjectBrushProfile, bool> biomeExpanded =
            new Dictionary<ObjectBrushProfile, bool>();

        private readonly Dictionary<ObjectBrushProfile.BiomeCategory, bool> categoryExpanded =
            new Dictionary<ObjectBrushProfile.BiomeCategory, bool>();

        private readonly Dictionary<ObjectBrushProfile.BiomeCategory, ReorderableList> itemLists =
            new Dictionary<ObjectBrushProfile.BiomeCategory, ReorderableList>();

        // Reused each repaint to collect indices of visible items in view mode (avoids per-frame allocation).
        private readonly List<int> visibleBuffer = new List<int>();

        // Reused when cycling the active item via hotkeys (avoids per-keystroke allocation).
        private readonly List<CycleEntry> cycleBuffer = new List<CycleEntry>();

        private GameObject previewInstance;
        private GameObject previewSource;

        private const float PaletteIconSize = 40f;
        private const float MinViewIconSize = 24f;
        private const float MaxViewIconSize = 96f;

        // Scene-view hotkeys (active only while the Scene view is focused). Backslash sits next to
        // the bracket keys and is unbound in the Scene view, unlike 'B' which the Tilemap brush uses.
        private const KeyCode ToggleBrushKey = KeyCode.Backslash;
        private const KeyCode NextItemKey = KeyCode.RightBracket;
        private const KeyCode PrevItemKey = KeyCode.LeftBracket;

        // Identifies one selectable palette slot (a category plus an item index within it).
        private readonly struct CycleEntry {
            public readonly ObjectBrushProfile.BiomeCategory Category;
            public readonly int Index;

            public CycleEntry(ObjectBrushProfile.BiomeCategory category, int index) {
                Category = category;
                Index = index;
            }
        }

        [MenuItem("Tools/Object Brush")]
        public static void Open() {
            GetWindow<ObjectBrushWindow>("Object Brush");
        }

        private void OnEnable() {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            config = ObjectBrushUtility.LoadOrCreateConfig(true);
        }

        private void OnActiveSceneChanged(Scene oldScene, Scene newScene) {
            Repaint();
        }

        private void OnDisable() {
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
            brushEnabled = false;
            DestroyPreviewInstance();
        }

        private void OnGUI() {
            if (config == null) {
                config = ObjectBrushUtility.LoadOrCreateConfig(true);
            }

            DrawTopBarGUI();
            EditorGUILayout.Space();
            DrawBrushSettingsGUI();
            EditorGUILayout.Space();
            DrawFilterGUI();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(GUIContent.none, GUI.skin.horizontalSlider);
            EditorGUILayout.Space(2f);

            DrawPaletteGUI();
        }

        // --- TOP BAR -------------------------------------------------------------

        private void DrawTopBarGUI() {
            EditorGUILayout.BeginHorizontal();

            string buttonText = brushEnabled ? "Disable Brush" : "Enable Brush";
            string buttonTooltip =
                "Toggle Scene painting on/off.\n" +
                "When enabled, left-click in Scene view places instances of the active prefab.\n" +
                "Hotkey: \\ (with the Scene view focused).";

            brushEnabled = GUILayout.Toggle(
                brushEnabled,
                new GUIContent(buttonText, buttonTooltip),
                "Button",
                GUILayout.Height(22),
                GUILayout.Width(110)
            );

            GUILayout.FlexibleSpace();

            editMode = GUILayout.Toggle(
                editMode,
                new GUIContent("Edit",
                    "Edit mode: show editable per-category item lists (add, remove, assign prefabs).\n" +
                    "When off, the palette is a compact preview grid for picking only."),
                "Button",
                GUILayout.Height(22),
                GUILayout.Width(60)
            );

            GUILayout.Space(4f);

            if (GUILayout.Button(
                    new GUIContent("Configure…", "Open the configuration window (World root, biomes, categories)."),
                    GUILayout.Height(22),
                    GUILayout.Width(90))) {
                ObjectBrushConfigWindow.Open();
            }

            EditorGUILayout.EndHorizontal();

            string prefabName = prefab != null ? prefab.name : "None";
            string prefabTooltip = prefab != null
                ? "Current active prefab selected from the palette."
                : "No prefab selected. Click a prefab in the palette to activate it.";

            EditorGUILayout.LabelField(
                new GUIContent("Active: " + prefabName, prefabTooltip),
                EditorStyles.miniLabel
            );

            EditorGUILayout.LabelField(
                new GUIContent("Hotkeys: \\ toggle  ·  [ prev  ·  ] next  (Scene view focused)",
                    "Scene-view hotkeys. Prev/next cycle through every palette item across all biomes."),
                EditorStyles.miniLabel
            );
        }

        // --- BRUSH SETTINGS ------------------------------------------------------

        private void DrawBrushSettingsGUI() {
            brushFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
                brushFoldout,
                new GUIContent("Brush Settings", "Core settings for placement and snapping.")
            );

            if (brushFoldout) {
                EditorGUI.indentLevel++;

                snapToGrid = EditorGUILayout.Toggle(
                    new GUIContent("Snap to Grid", "Snap placed objects to a grid."),
                    snapToGrid
                );

                using (new EditorGUI.DisabledScope(!snapToGrid)) {
                    gridSize = EditorGUILayout.FloatField(
                        new GUIContent("Grid Size", "Step size for grid snapping."),
                        gridSize
                    );
                    if (gridSize <= 0f) {
                        gridSize = 0.1f;
                    }
                }

                EditorGUILayout.Space(2f);

                viewIconSize = EditorGUILayout.Slider(
                    new GUIContent("Preview Size",
                        "Size of item previews in View mode. No effect in Edit mode."),
                    viewIconSize,
                    MinViewIconSize,
                    MaxViewIconSize
                );

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // --- FILTER --------------------------------------------------------------

        private void DrawFilterGUI() {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                new GUIContent("Filter", "Filter palette items by prefab name (case-insensitive)."),
                GUILayout.Width(40)
            );

            filterText = EditorGUILayout.TextField(filterText);
            EditorGUILayout.EndHorizontal();
        }

        // --- PALETTE (BIOME -> CATEGORY -> ITEMS) --------------------------------

        private void DrawPaletteGUI() {
            if (config == null || config.biomes == null || config.biomes.Count == 0) {
                EditorGUILayout.HelpBox(
                    "No biomes referenced. Open Configure… to add biome profiles and categories.",
                    MessageType.Info
                );
                return;
            }

            categoriesScroll = EditorGUILayout.BeginScrollView(categoriesScroll);

            foreach (ObjectBrushProfile biome in config.biomes) {
                if (biome == null) {
                    continue;
                }

                bool biomeOpen = GetExpanded(biomeExpanded, biome, true);
                biomeOpen = EditorGUILayout.BeginFoldoutHeaderGroup(biomeOpen, biome.name);
                biomeExpanded[biome] = biomeOpen;

                if (biomeOpen) {
                    EditorGUI.indentLevel++;
                    DrawBiomeCategories(biome);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndFoldoutHeaderGroup();
                EditorGUILayout.Space(2f);
            }

            EditorGUILayout.EndScrollView();

            ScrollToActiveIfRequested();
        }

        private void DrawBiomeCategories(ObjectBrushProfile biome) {
            if (biome.categories == null || biome.categories.Count == 0) {
                EditorGUILayout.LabelField("No categories. Add them in Configure…", EditorStyles.miniLabel);
                return;
            }

            foreach (ObjectBrushProfile.BiomeCategory category in biome.categories) {
                bool catOpen = GetExpanded(categoryExpanded, category, true);
                catOpen = EditorGUILayout.Foldout(catOpen, category.name, true);
                categoryExpanded[category] = catOpen;

                if (!catOpen) {
                    continue;
                }

                EditorGUI.indentLevel++;

                if (editMode) {
                    DrawCategoryEdit(biome, category);
                } else {
                    DrawCategoryView(category);
                }

                EditorGUI.indentLevel--;
            }
        }

        // Edit mode: editable item list with add / remove / assign.
        private void DrawCategoryEdit(ObjectBrushProfile biome, ObjectBrushProfile.BiomeCategory category) {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                new GUIContent("Items: " + category.items.Count, "Number of prefabs in this category."),
                GUILayout.Width(120)
            );

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(
                    new GUIContent("+ Add Item", "Add a new prefab slot to this category."),
                    GUILayout.Width(110))) {
                Undo.RecordObject(biome, "Add Item");
                category.items.Add(null);
                EditorUtility.SetDirty(biome);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3f);

            ReorderableList list = EnsureItemList(biome, category);
            list.elementHeight = PaletteIconSize + 8f;
            list.DoLayoutList();
        }

        // View mode: read-only wrapping grid of previews for picking only.
        private void DrawCategoryView(ObjectBrushProfile.BiomeCategory category) {
            string filterLower = string.IsNullOrEmpty(filterText) ? null : filterText.ToLowerInvariant();

            visibleBuffer.Clear();
            for (int i = 0; i < category.items.Count; i++) {
                GameObject item = category.items[i];
                if (item == null) {
                    continue;
                }

                if (filterLower != null && !item.name.ToLowerInvariant().Contains(filterLower)) {
                    continue;
                }

                visibleBuffer.Add(i);
            }

            if (visibleBuffer.Count == 0) {
                EditorGUILayout.LabelField("—", EditorStyles.miniLabel);
                return;
            }

            const float spacing = 4f;
            float iconSize = viewIconSize;
            float indent = EditorGUI.indentLevel * 15f;
            float available = EditorGUIUtility.currentViewWidth - indent - 26f; // indent + scrollbar/right margin
            int columns = Mathf.Max(1, Mathf.FloorToInt((available + spacing) / (iconSize + spacing)));

            int k = 0;
            while (k < visibleBuffer.Count) {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(indent);

                for (int c = 0; c < columns && k < visibleBuffer.Count; c++, k++) {
                    DrawViewIcon(category, visibleBuffer[k], iconSize);
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(3f);
        }

        private void DrawViewIcon(ObjectBrushProfile.BiomeCategory category, int index, float size) {
            GameObject item = category.items[index];

            Texture2D preview = AssetPreview.GetAssetPreview(item);
            if (preview == null) {
                preview = AssetPreview.GetMiniThumbnail(item);
            }

            GUIContent content = preview != null
                ? new GUIContent(preview, item.name)
                : new GUIContent(item.name, item.name);

            bool clicked = GUILayout.Button(content, GUILayout.Width(size), GUILayout.Height(size));
            Rect iconRect = GUILayoutUtility.GetLastRect();

            if (category == activeCategory && index == activeItemIndex) {
                EditorGUI.DrawRect(iconRect, new Color(0.7f, 0.9f, 1f, 0.35f));
                CaptureActiveRect(iconRect);
            }

            if (clicked) {
                SetActiveItem(category, index);
            }
        }

        private ReorderableList EnsureItemList(ObjectBrushProfile biome, ObjectBrushProfile.BiomeCategory category) {
            if (itemLists.TryGetValue(category, out ReorderableList existing)) {
                existing.list = category.items;
                return existing;
            }

            ReorderableList list = new ReorderableList(
                category.items,
                typeof(GameObject),
                true,
                false,
                false,
                false
            ) {
                drawElementCallback = (rect, index, isActive, isFocused) =>
                    DrawPaletteItem(biome, category, rect, index, isActive),
                onReorderCallback = l => EditorUtility.SetDirty(biome)
            };

            itemLists[category] = list;
            return list;
        }

        private void DrawPaletteItem(ObjectBrushProfile biome, ObjectBrushProfile.BiomeCategory category,
            Rect rect, int index, bool isActive) {
            if (index < 0 || index >= category.items.Count) {
                return;
            }

            GameObject item = category.items[index];

            string filterLower = string.IsNullOrEmpty(filterText) ? null : filterText.ToLowerInvariant();
            bool matchesFilter = true;
            if (filterLower != null && item != null) {
                matchesFilter = item.name.ToLowerInvariant().Contains(filterLower);
            }

            Color oldGuiColor = GUI.color;
            if (!matchesFilter && filterLower != null) {
                GUI.color = new Color(1f, 1f, 1f, 0.4f);
            }

            rect.y += 2f;
            rect.height -= 4f;

            float padding = 4f;

            Rect iconRect = new Rect(rect.x + padding, rect.y + padding, PaletteIconSize, PaletteIconSize);

            float trashWidth = 24f;
            float fieldRightPadding = 4f;
            Rect fieldRect = new Rect(
                iconRect.xMax + 4f,
                rect.y + padding + (PaletteIconSize - EditorGUIUtility.singleLineHeight) * 0.5f,
                rect.width - iconRect.width - trashWidth - 3f * fieldRightPadding,
                EditorGUIUtility.singleLineHeight
            );

            Rect trashRect = new Rect(
                rect.xMax - trashWidth - padding,
                rect.y + padding + (PaletteIconSize - 18f) * 0.5f,
                trashWidth,
                18f
            );

            if (category == activeCategory && index == activeItemIndex) {
                EditorGUI.DrawRect(rect, new Color(0.7f, 0.9f, 1f, 0.3f));
                CaptureActiveRect(rect);
            }

            Texture2D preview = null;
            if (item != null) {
                preview = AssetPreview.GetAssetPreview(item) ?? AssetPreview.GetMiniThumbnail(item);
            }

            GUIContent iconContent = preview != null
                ? new GUIContent(preview, item.name)
                : new GUIContent("None", "Empty slot");

            if (GUI.Button(iconRect, iconContent) && item != null) {
                SetActiveItem(category, index);
            }

            GameObject newItem = (GameObject)EditorGUI.ObjectField(fieldRect, item, typeof(GameObject), false);
            if (newItem != item) {
                Undo.RecordObject(biome, "Assign Item");
                category.items[index] = newItem;
                EditorUtility.SetDirty(biome);

                if (category == activeCategory && index == activeItemIndex) {
                    prefab = newItem;
                }
            }

            GUIContent trashIcon = EditorGUIUtility.IconContent("TreeEditor.Trash");
            if (GUI.Button(trashRect, trashIcon)) {
                Undo.RecordObject(biome, "Remove Item");
                category.items.RemoveAt(index);
                EditorUtility.SetDirty(biome);

                if (category == activeCategory) {
                    if (activeItemIndex == index) {
                        activeItemIndex = -1;
                    } else if (activeItemIndex > index) {
                        activeItemIndex--;
                    }
                }
            }

            GUI.color = oldGuiColor;
        }

        private void SetActiveItem(ObjectBrushProfile.BiomeCategory category, int index) {
            activeCategory = category;
            activeItemIndex = index;
            activeParentPath = ObjectBrushUtility.ResolveParentPath(category);

            GameObject item = category.items[index];
            if (item != null) {
                prefab = item;
            }

            // Make sure the slot is reachable, then request a scroll to it on the next repaint.
            ObjectBrushProfile biome = FindBiomeOf(category);
            if (biome != null) {
                biomeExpanded[biome] = true;
            }
            categoryExpanded[category] = true;
            scrollToActive = true;
        }

        private ObjectBrushProfile FindBiomeOf(ObjectBrushProfile.BiomeCategory category) {
            if (config == null || config.biomes == null) {
                return null;
            }

            foreach (ObjectBrushProfile biome in config.biomes) {
                if (biome != null && biome.categories != null && biome.categories.Contains(category)) {
                    return biome;
                }
            }

            return null;
        }

        // Records the active slot's rect during repaint so the palette can scroll to it afterwards.
        private void CaptureActiveRect(Rect rect) {
            if (scrollToActive && Event.current.type == EventType.Repaint) {
                pendingActiveRect = rect;
                hasPendingActiveRect = true;
            }
        }

        // Scrolls the palette so the active slot is visible. Called right after EndScrollView, where
        // GUILayoutUtility.GetLastRect() returns the scroll viewport and item rects are in content space.
        private void ScrollToActiveIfRequested() {
            if (!scrollToActive || Event.current.type != EventType.Repaint) {
                return;
            }

            if (hasPendingActiveRect) {
                float viewHeight = GUILayoutUtility.GetLastRect().height;

                if (pendingActiveRect.y < categoriesScroll.y) {
                    categoriesScroll.y = pendingActiveRect.y;
                } else if (pendingActiveRect.yMax > categoriesScroll.y + viewHeight) {
                    categoriesScroll.y = pendingActiveRect.yMax - viewHeight;
                }

                if (categoriesScroll.y < 0f) {
                    categoriesScroll.y = 0f;
                }

                Repaint();
            }

            // Clear the request even if the slot was not drawn (e.g. hidden by the filter).
            scrollToActive = false;
            hasPendingActiveRect = false;
        }

        private static bool GetExpanded<TKey>(Dictionary<TKey, bool> map, TKey key, bool defaultValue) {
            return !map.TryGetValue(key, out bool value) ? defaultValue : value;
        }

        // --- SCENE GUI / BRUSH LOGIC --------------------------------------------

        // Processes Scene-view hotkeys. Runs before the enabled/prefab checks so the toggle works
        // even while the brush is off. Returns true when the event was consumed.
        private bool HandleShortcuts(Event e) {
            if (e.type != EventType.KeyDown || e.alt || e.control || e.command || e.shift) {
                return false;
            }

            switch (e.keyCode) {
                case ToggleBrushKey:
                    brushEnabled = !brushEnabled;
                    if (!brushEnabled) {
                        DestroyPreviewInstance();
                    }
                    break;

                case NextItemKey:
                    CycleActiveItem(1);
                    break;

                case PrevItemKey:
                    CycleActiveItem(-1);
                    break;

                default:
                    return false;
            }

            Repaint();
            SceneView.RepaintAll();
            e.Use();
            return true;
        }

        // Builds a flat list of selectable items across every biome and category (respecting the
        // active name filter) and moves the active selection by 'direction' (+1 next, -1 prev), wrapping.
        private void CycleActiveItem(int direction) {
            if (config == null || config.biomes == null) {
                return;
            }

            cycleBuffer.Clear();
            string filterLower = string.IsNullOrEmpty(filterText) ? null : filterText.ToLowerInvariant();

            foreach (ObjectBrushProfile biome in config.biomes) {
                if (biome == null || biome.categories == null) {
                    continue;
                }

                foreach (ObjectBrushProfile.BiomeCategory category in biome.categories) {
                    if (category == null || category.items == null) {
                        continue;
                    }

                    for (int i = 0; i < category.items.Count; i++) {
                        GameObject item = category.items[i];
                        if (item == null) {
                            continue;
                        }

                        if (filterLower != null && !item.name.ToLowerInvariant().Contains(filterLower)) {
                            continue;
                        }

                        cycleBuffer.Add(new CycleEntry(category, i));
                    }
                }
            }

            if (cycleBuffer.Count == 0) {
                return;
            }

            int current = -1;
            for (int i = 0; i < cycleBuffer.Count; i++) {
                if (cycleBuffer[i].Category == activeCategory && cycleBuffer[i].Index == activeItemIndex) {
                    current = i;
                    break;
                }
            }

            int next;
            if (current < 0) {
                // Nothing selected yet (or the selection was filtered out): step in from the matching end.
                next = direction > 0 ? 0 : cycleBuffer.Count - 1;
            } else {
                next = (current + direction + cycleBuffer.Count) % cycleBuffer.Count;
            }

            CycleEntry entry = cycleBuffer[next];
            SetActiveItem(entry.Category, entry.Index);
        }

        private void OnSceneGUI(SceneView sceneView) {
            Event e = Event.current;

            if (HandleShortcuts(e)) {
                return;
            }

            if (!brushEnabled || prefab == null) {
                DestroyPreviewInstance();
                return;
            }

            if (e.alt) {
                return;
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (!TryGetHitPosition(ray, out Vector3 hitPos)) {
                DestroyPreviewInstance();
                return;
            }

            bool useSnap = snapToGrid;
            if (e.control) {
                useSnap = !useSnap;
            }

            if (useSnap) {
                hitPos = SnapPosition(hitPos, gridSize);
            }

            UpdatePreviewInstance(hitPos);

            Handles.color = Color.yellow;
            DrawCross(hitPos, 0.15f);

            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt) {
                PlaceObject(hitPos);
                e.Use();
            }

            SceneView.RepaintAll();
        }

        private void DrawCross(Vector3 pos, float size) {
            Handles.DrawLine(pos + Vector3.left * size, pos + Vector3.right * size);
            Handles.DrawLine(pos + Vector3.up * size, pos + Vector3.down * size);
        }

        private bool TryGetHitPosition(Ray ray, out Vector3 hitPos) {
            hitPos = Vector3.zero;

            if (Physics.Raycast(ray, out RaycastHit hit3D, 1000f)) {
                hitPos = hit3D.point;
                return true;
            }

            RaycastHit2D hit2D = Physics2D.GetRayIntersection(ray, 1000f);
            if (hit2D.collider != null) {
                hitPos = hit2D.point;
                return true;
            }

            Plane plane = new Plane(Vector3.forward, Vector3.zero);
            if (plane.Raycast(ray, out float distance)) {
                hitPos = ray.GetPoint(distance);
                return true;
            }

            return false;
        }

        private Vector3 SnapPosition(Vector3 pos, float size) {
            pos.x = Mathf.Round(pos.x / size) * size;
            pos.y = Mathf.Round(pos.y / size) * size;
            pos.z = Mathf.Round(pos.z / size) * size;
            return pos;
        }

        private void PlaceObject(Vector3 pos) {
            if (prefab == null) {
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (instance == null) {
                return;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Place Object");
            instance.transform.position = pos;

            string worldRootName = config != null ? config.worldRootName : "World";
            Transform targetParent = ObjectBrushUtility.ResolveOrCreateParent(scene, worldRootName, activeParentPath);
            if (targetParent != null) {
                instance.transform.SetParent(targetParent, true);
            }

            string baseName = prefab.name;
            int nextIndex = GetNextInstanceIndex(baseName, scene);
            instance.name = $"{baseName}_{nextIndex}";
        }

        private void UpdatePreviewInstance(Vector3 pos) {
            if (previewInstance == null || previewSource != prefab) {
                DestroyPreviewInstance();

                previewSource = prefab;
                Scene scene = SceneManager.GetActiveScene();
                previewInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);

                if (previewInstance == null) {
                    return;
                }

                previewInstance.name = prefab.name + " (Preview)";
                previewInstance.hideFlags = HideFlags.HideAndDontSave;

                SpriteRenderer[] spriteRenderers = previewInstance.GetComponentsInChildren<SpriteRenderer>();
                foreach (SpriteRenderer sr in spriteRenderers) {
                    Color c = sr.color;
                    c.a = 0.3f;
                    sr.color = c;
                }

                Collider[] colliders3D = previewInstance.GetComponentsInChildren<Collider>();
                foreach (Collider col in colliders3D) {
                    col.enabled = false;
                }

                Collider2D[] colliders2D = previewInstance.GetComponentsInChildren<Collider2D>();
                foreach (Collider2D col in colliders2D) {
                    col.enabled = false;
                }
            }

            if (previewInstance != null) {
                previewInstance.transform.position = pos;
            }
        }

        private void DestroyPreviewInstance() {
            if (previewInstance != null) {
                DestroyImmediate(previewInstance);
                previewInstance = null;
                previewSource = null;
            }
        }

        private int GetNextInstanceIndex(string baseName, Scene scene) {
            int maxIndex = 0;

            GameObject[] roots = scene.GetRootGameObjects();
            Stack<Transform> stack = new Stack<Transform>();

            for (int i = 0; i < roots.Length; i++) {
                stack.Push(roots[i].transform);
            }

            while (stack.Count > 0) {
                Transform t = stack.Pop();
                string name = t.gameObject.name;

                if (!name.StartsWith(baseName, StringComparison.Ordinal)) {
                    for (int i = 0; i < t.childCount; i++) {
                        stack.Push(t.GetChild(i));
                    }

                    continue;
                }

                string suffix = name.Substring(baseName.Length);

                // Supported options: "Barrel1", "Barrel_1", "Barrel 1"
                if (!string.IsNullOrEmpty(suffix) && (suffix[0] == '_' || suffix[0] == ' ')) {
                    suffix = suffix.Substring(1);
                }

                if (!string.IsNullOrEmpty(suffix) && int.TryParse(suffix, out int index)) {
                    if (index > maxIndex) {
                        maxIndex = index;
                    }
                }

                for (int i = 0; i < t.childCount; i++) {
                    stack.Push(t.GetChild(i));
                }
            }

            return maxIndex + 1;
        }
    }
}
