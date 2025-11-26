using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace Editor.ObjectBrush {
    /// <summary>
    /// Editor window that provides an object painting brush for the Scene view.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Object Brush lets you quickly place prefab instances into the Scene view,
    /// using a small palette similar to a tile palette but for arbitrary GameObjects.
    /// </para>
    ///
    /// <para><b>Opening</b></para>
    /// <para>
    /// Open the window from the main menu: <c>Tools &gt; Object Brush</c>.
    /// </para>
    ///
    /// <para><b>Basic usage</b></para>
    /// <list type="number">
    ///   <item>
    ///     <description>
    ///       Open the Object Brush window and enable the brush using the
    ///       <c>Enable Brush</c> toggle button at the top.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       Add one or more categories and assign prefabs to the palette slots
    ///       inside each category. Click on a prefab preview to make it the active brush.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       Optionally set a <c>Global Parent</c> and/or a <c>Default Parent</c>
    ///       for each category. New instances will be parented under these transforms.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       In the Scene view, move the mouse to position the preview, then
    ///       left-click to place an instance of the active prefab.
    ///       Hold <c>Ctrl</c> to temporarily invert the grid snapping setting.
    ///     </description>
    ///   </item>
    /// </list>
    ///
    /// <para><b>Grid snapping</b></para>
    /// <para>
    /// When <c>Snap to Grid</c> is enabled, placed objects are snapped to a regular grid
    /// with the configured <c>Grid Size</c>. Holding <c>Ctrl</c> temporarily inverts
    /// the snapping (enabled becomes disabled and vice versa).
    /// </para>
    ///
    /// <para><b>Categories and filtering</b></para>
    /// <para>
    /// Categories group palette items (prefabs) by biome, theme or usage.
    /// Each category can be expanded as an accordion section to show its palette.
    /// The filter field above categories filters items by prefab name across all categories.
    /// </para>
    ///
    /// <para><b>Biome profiles</b></para>
    /// <para>
    /// A <see cref="ObjectBrushProfile"/> asset can be used to store and reuse
    /// category and palette setups for different biomes or levels. The profile stores:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <description>Category names</description>
    ///   </item>
    ///   <item>
    ///     <description>Prefab references in each category</description>
    ///   </item>
    /// </list>
    /// <para>
    /// It does <b>not</b> store scene-specific parents or brush state.
    /// Use the <c>Load</c> button to replace the current palette with the selected profile,
    /// and the <c>Save</c> button to overwrite the profile with the current categories
    /// and palette items.
    /// </para>
    /// </remarks>
    public class ObjectBrushWindow : EditorWindow {
        [Serializable]
        private class PaletteCategory {
            public string name = "Category";
            public Transform defaultParent;
            public List<GameObject> items = new List<GameObject>();

            [NonSerialized]
            public ReorderableList List;

            [NonSerialized]
            public bool IsExpanded = true;
            
            [NonSerialized]
            public bool propertiesExpanded = true;
        }

        [SerializeField]
        private GameObject prefab;

        [SerializeField]
        private Transform globalParent;

        [SerializeField]
        private bool snapToGrid = true;

        [SerializeField]
        private float gridSize = 1f;

        [SerializeField]
        private bool brushEnabled;

        [FormerlySerializedAs("biomeProfile")]
        [SerializeField]
        private ObjectBrushProfile profile;

        [SerializeField]
        private List<PaletteCategory> categories = new List<PaletteCategory>();

        [SerializeField]
        private int selectedCategoryIndex;

        [SerializeField]
        private int selectedItemIndex = -1;

        [SerializeField]
        private string filterText = "";

        [SerializeField]
        private Vector2 categoriesScroll;

        [SerializeField]
        private bool biomeFoldout = true;

        [SerializeField]
        private bool brushFoldout = true;

        private GameObject previewInstance;
        private GameObject previewSource;

        private const float PaletteIconSize = 40f;

        [MenuItem("Tools/Object Brush")]
        public static void Open() {
            GetWindow<ObjectBrushWindow>("Object Brush");
        }

        private void OnEnable() {
            SceneView.duringSceneGui += OnSceneGUI;

            if (categories == null) {
                categories = new List<PaletteCategory>();
            }

            if (categories.Count == 0) {
                categories.Add(new PaletteCategory { name = "Default" });
            }

            selectedCategoryIndex = Mathf.Clamp(selectedCategoryIndex, 0, categories.Count - 1);

            for (int i = 0; i < categories.Count; i++) {
                EnsureReorderableList(categories[i], i);
            }
        }

        private void OnDisable() {
            SceneView.duringSceneGui -= OnSceneGUI;
            brushEnabled = false;
            DestroyPreviewInstance();
        }

        private void OnGUI() {
            DrawTopBarGUI();
            EditorGUILayout.Space();
            DrawBiomeGUI();
            EditorGUILayout.Space();
            DrawBrushSettingsGUI();
            EditorGUILayout.Space();
            DrawFilterAndCategoriesHeaderGUI();
            EditorGUILayout.Space(2f);
            DrawCategoriesAndPalettesGUI();
        }

        // --- TOP BAR -------------------------------------------------------------

        private void DrawTopBarGUI() {
            EditorGUILayout.BeginHorizontal();

            string buttonText = brushEnabled ? "Disable Brush" : "Enable Brush";
            string buttonTooltip =
                "Toggle Scene painting on/off.\n" +
                "When enabled, left-click in Scene view places instances of the active prefab.";

            bool newEnabled = GUILayout.Toggle(
                brushEnabled,
                new GUIContent(buttonText, buttonTooltip),
                "Button",
                GUILayout.Height(22),
                GUILayout.Width(110)
            );

            brushEnabled = newEnabled;

            // Active prefab label right next to the button
            string prefabName = prefab != null ? prefab.name : "None";
            string prefabTooltip = prefab != null
                ? "Current active prefab selected from the palette."
                : "No prefab selected. Click a prefab in the palette to activate it.";

            GUILayout.Space(8f);
            EditorGUILayout.LabelField(
                new GUIContent("Active: " + prefabName, prefabTooltip),
                EditorStyles.miniLabel
            );

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // --- BIOME PROFILE -------------------------------------------------------

        private void DrawBiomeGUI() {
            biomeFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
                biomeFoldout,
                new GUIContent("Biome Profile", "Store and reuse palette data as biome profiles.")
            );

            if (biomeFoldout) {
                EditorGUI.indentLevel++;

                EditorGUILayout.BeginHorizontal();
                profile = (ObjectBrushProfile)EditorGUILayout.ObjectField(
                    new GUIContent(
                        "Profile",
                        "Biome profile asset (.asset) that stores palette categories and their prefabs.\n\n" +
                        "Use 'Load' to replace the current palette with data from this profile.\n" +
                        "Use 'Save' to overwrite the profile with the current palette."
                    ),
                    profile,
                    typeof(ObjectBrushProfile),
                    false
                );

                using (new EditorGUI.DisabledScope(profile == null)) {
                    if (GUILayout.Button(
                            new GUIContent("Load", "Replace current categories and items with data from this profile."),
                            GUILayout.Width(50))) {
                        LoadFromBiomeProfile();
                    }

                    if (GUILayout.Button(
                            new GUIContent("Save", "Overwrite this profile with the current categories and items."),
                            GUILayout.Width(50))) {
                        SaveToBiomeProfile();
                    }
                }

                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void LoadFromBiomeProfile() {
            if (profile == null) {
                return;
            }

            categories.Clear();

            foreach (var bc in profile.categories) {
                var cat = new PaletteCategory {
                    name = bc.name,
                    defaultParent = null,
                    items = new List<GameObject>(bc.items)
                };
                categories.Add(cat);
            }

            if (categories.Count == 0) {
                categories.Add(new PaletteCategory { name = "Default" });
            }

            selectedCategoryIndex = 0;
            selectedItemIndex = -1;

            for (int i = 0; i < categories.Count; i++) {
                EnsureReorderableList(categories[i], i);
            }
        }

        private void SaveToBiomeProfile() {
            if (profile == null) {
                EditorUtility.DisplayDialog(
                    "No Biome Profile",
                    "Assign a biome profile asset first.",
                    "OK"
                );
                return;
            }

            // If profile already contains data, ask for confirmation before overwriting.
            bool hasExistingData = profile.categories != null && profile.categories.Count > 0;
            if (hasExistingData) {
                bool confirm = EditorUtility.DisplayDialog(
                    "Overwrite Biome Profile",
                    "This will overwrite the existing categories and items in the selected biome profile.\n\n" +
                    "Are you sure you want to save the current palette into this profile?",
                    "Overwrite",
                    "Cancel"
                );

                if (!confirm) {
                    return;
                }
            }

            if (profile.categories != null) {
                profile.categories.Clear();

                foreach (var cat in categories) {
                    var bc = new ObjectBrushProfile.BiomeCategory {
                        name = cat.name,
                        items = new List<GameObject>(cat.items)
                    };
                    profile.categories.Add(bc);
                }
            }

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        // --- BRUSH SETTINGS ------------------------------------------------------

        private void DrawBrushSettingsGUI() {
            brushFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
                brushFoldout,
                new GUIContent("Brush Settings", "Core settings for placement and snapping.")
            );

            if (brushFoldout) {
                EditorGUI.indentLevel++;

                globalParent = (Transform)EditorGUILayout.ObjectField(
                    new GUIContent("Global Parent", "Optional fallback parent used when category has no default parent."),
                    globalParent,
                    typeof(Transform),
                    true
                );

                EditorGUILayout.Space(2f);

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

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // --- FILTER + CATEGORIES HEADER -----------------------------------------

        private void DrawFilterAndCategoriesHeaderGUI() {
            // Filter (one line)
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                new GUIContent("Filter", "Filter palette items by prefab name (case-insensitive)."),
                GUILayout.Width(40)
            );

            filterText = EditorGUILayout.TextField(filterText);

            EditorGUILayout.EndHorizontal();

            // Categories + buttons in one line
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                new GUIContent("Categories", "Palette categories grouped by biome or usage."),
                EditorStyles.boldLabel
            );
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(new GUIContent("+", "Add new category."), GUILayout.Width(24))) {
                var cat = new PaletteCategory { name = "Category " + categories.Count };
                categories.Add(cat);
                selectedCategoryIndex = categories.Count - 1;
                selectedItemIndex = -1;
                EnsureReorderableList(cat, selectedCategoryIndex);
            }

            using (new EditorGUI.DisabledScope(categories.Count <= 1)) {
                if (GUILayout.Button(new GUIContent("-", "Remove the currently selected category."),
                        GUILayout.Width(24))) {
                    if (selectedCategoryIndex >= 0 && selectedCategoryIndex < categories.Count) {
                        categories.RemoveAt(selectedCategoryIndex);
                        if (categories.Count == 0) {
                            categories.Add(new PaletteCategory { name = "Default" });
                        }

                        selectedCategoryIndex = Mathf.Clamp(selectedCategoryIndex, 0, categories.Count - 1);
                        selectedItemIndex = -1;
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        // --- CATEGORIES + PALETTES (ACCORDION + SCROLL) -------------------------

        private void DrawCategoriesAndPalettesGUI() {
            if (categories.Count == 0) {
                return;
            }

            categoriesScroll = EditorGUILayout.BeginScrollView(categoriesScroll);

            for (int i = 0; i < categories.Count; i++) {
                PaletteCategory category = categories[i];
                EnsureReorderableList(category, i);

                var i1 = i;
                category.IsExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(
                    category.IsExpanded,
                    category.name,
                    null,
                    rect => {
                        if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition)) {
                            selectedCategoryIndex = i1;
                        }
                    }
                );

                if (category.IsExpanded) {
                    EditorGUI.indentLevel++;

                    // Обычный Foldout для настроек категории (НЕ header group)
                    category.propertiesExpanded = EditorGUILayout.Foldout(
                        category.propertiesExpanded,
                        new GUIContent("Category Settings", "Name and default parent for this category."),
                        true
                    );

                    if (category.propertiesExpanded) {
                        EditorGUI.indentLevel++;

                        category.name = EditorGUILayout.TextField(
                            new GUIContent("Name", "Category name shown in the header."),
                            category.name
                        );

                        category.defaultParent = (Transform)EditorGUILayout.ObjectField(
                            new GUIContent("Default Parent", "Default parent for instantiated objects in this category."),
                            category.defaultParent,
                            typeof(Transform),
                            true
                        );

                        EditorGUI.indentLevel--;
                    }

                    EditorGUILayout.Space(4f);

                    // Строка: Items Count + Add Item
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(
                        new GUIContent("Items: " + category.items.Count, "Number of prefabs in this category."),
                        GUILayout.Width(120)
                    );

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button(
                            new GUIContent("+ Add Item", "Add new prefab slot to this category."),
                            GUILayout.Width(110))) {
                        category.items.Add(null);
                        category.List.list = category.items;
                    }

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space(3f);

                    // Список элементов палитры
                    category.List.elementHeight = PaletteIconSize + 8f;
                    category.List.DoLayoutList();

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndFoldoutHeaderGroup();
                EditorGUILayout.Space(2f);
            }

            EditorGUILayout.EndScrollView();
        }

        private void EnsureReorderableList(PaletteCategory category, int categoryIndex) {
            if (category.List != null) {
                category.List.list = category.items;
                return;
            }

            category.List = new ReorderableList(
                category.items,
                typeof(GameObject),
                true, // draggable
                false, // display header (we draw our own)
                false, // display add button
                false // display remove button
            ) {
                drawElementCallback = (rect, index, isActive, isFocused) => {
                    DrawPaletteElement(category, categoryIndex, rect, index, isActive);
                },

                onSelectCallback = list => {
                    selectedCategoryIndex = categoryIndex;
                    selectedItemIndex = list.index;

                    if (selectedItemIndex >= 0 && selectedItemIndex < category.items.Count) {
                        GameObject item = category.items[selectedItemIndex];

                        if (item != null) {
                            prefab = item;
                        }
                    }
                }
            };
        }

        private void DrawPaletteElement(PaletteCategory category, int categoryIndex, Rect rect, int index,
            bool isActive) {
            if (index < 0 || index >= category.items.Count) {
                return;
            }

            GameObject item = category.items[index];

            string filterLower = string.IsNullOrEmpty(filterText)
                ? null
                : filterText.ToLowerInvariant();

            bool matchesFilter = true;

            if (filterLower != null && item != null) {
                string nameLower = item.name.ToLowerInvariant();
                matchesFilter = nameLower.Contains(filterLower);
            }

            Color oldGuiColor = GUI.color;
            if (!matchesFilter && filterLower != null) {
                GUI.color = new Color(1f, 1f, 1f, 0.4f);
            }

            rect.y += 2f;
            rect.height -= 4f;

            float padding = 4f;

            Rect iconRect = new Rect(
                rect.x + padding,
                rect.y + padding,
                PaletteIconSize,
                PaletteIconSize
            );

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

            if (isActive && selectedCategoryIndex == categoryIndex) {
                Color highlight = new Color(0.7f, 0.9f, 1f, 0.3f);
                EditorGUI.DrawRect(rect, highlight);
            }

            Texture2D preview = null;
            if (item != null) {
                preview = AssetPreview.GetAssetPreview(item) ?? AssetPreview.GetMiniThumbnail(item);
            }

            GUIContent iconContent;
            if (preview != null) {
                iconContent = new GUIContent(preview, item.name);
            } else {
                iconContent = new GUIContent("None", "Empty slot");
            }

            if (GUI.Button(iconRect, iconContent)) {
                selectedCategoryIndex = categoryIndex;
                category.List.index = index;
                selectedItemIndex = index;
                if (item != null) {
                    prefab = item;
                }
            }

            GameObject newItem = (GameObject)EditorGUI.ObjectField(
                fieldRect,
                item,
                typeof(GameObject),
                false
            );

            if (newItem != item) {
                category.items[index] = newItem;
                if (isActive && newItem != null) {
                    prefab = newItem;
                }
            }

            GUIContent trashIcon = EditorGUIUtility.IconContent("TreeEditor.Trash");
            if (GUI.Button(trashRect, trashIcon)) {
                category.items.RemoveAt(index);
                category.List.list = category.items;

                if (selectedCategoryIndex == categoryIndex) {
                    if (selectedItemIndex == index) {
                        selectedItemIndex = -1;
                        category.List.index = -1;
                    } else if (selectedItemIndex > index) {
                        selectedItemIndex--;
                        category.List.index = selectedItemIndex;
                    }
                }
            }

            GUI.color = oldGuiColor;
        }

        // --- SCENE GUI / BRUSH LOGIC --------------------------------------------

        private void OnSceneGUI(SceneView sceneView) {
            if (!brushEnabled) {
                DestroyPreviewInstance();
                return;
            }

            if (prefab == null) {
                DestroyPreviewInstance();
                return;
            }

            Event e = Event.current;

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

            Transform targetParent = null;

            if (selectedCategoryIndex >= 0 && selectedCategoryIndex < categories.Count) {
                targetParent = categories[selectedCategoryIndex].defaultParent;
            }

            if (targetParent == null) {
                targetParent = globalParent;
            }

            if (targetParent != null) {
                instance.transform.SetParent(targetParent, true);
            }
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
    }
}
