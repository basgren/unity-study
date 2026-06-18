using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Editor.ObjectBrush {
    /// <summary>
    /// Editor window for configuring the Object Brush structure: the shared World root
    /// name and the set of biome profiles together with their categories and parent paths.
    /// </summary>
    /// <remarks>
    /// Prefab assignment lives in the main <see cref="ObjectBrushWindow"/>; this window only
    /// edits structure. All edits are written directly into the shared
    /// <see cref="ObjectBrushConfig"/> asset and the referenced <see cref="ObjectBrushProfile"/>
    /// assets.
    /// </remarks>
    public class ObjectBrushConfigWindow : EditorWindow {
        private ObjectBrushConfig config;

        private Vector2 scroll;

        [SerializeField]
        private ObjectBrushProfile biomeToAdd;

        private readonly Dictionary<ObjectBrushProfile, bool> biomeExpanded =
            new Dictionary<ObjectBrushProfile, bool>();

        private readonly Dictionary<ObjectBrushProfile, ReorderableList> categoryLists =
            new Dictionary<ObjectBrushProfile, ReorderableList>();

        [MenuItem("Tools/Object Brush Configuration")]
        public static void Open() {
            GetWindow<ObjectBrushConfigWindow>("Object Brush Config");
        }

        private void OnEnable() {
            config = ObjectBrushUtility.LoadOrCreateConfig(true);
        }

        private void OnGUI() {
            if (config == null) {
                config = ObjectBrushUtility.LoadOrCreateConfig(true);
            }

            DrawWorldRootGUI();
            EditorGUILayout.Space();
            DrawBiomesGUI();
        }

        // --- WORLD ROOT ----------------------------------------------------------

        private void DrawWorldRootGUI() {
            EditorGUILayout.LabelField(
                new GUIContent("World", "Shared parenting convention used in every scene."),
                EditorStyles.boldLabel
            );

            EditorGUI.BeginChangeCheck();
            string newRoot = EditorGUILayout.TextField(
                new GUIContent(
                    "World Root Name",
                    "Name of the root object in each scene under which placed objects are nested.\n" +
                    "Leave empty to place directly at scene root."
                ),
                config.worldRootName
            );

            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(config, "Edit World Root Name");
                config.worldRootName = newRoot;
                EditorUtility.SetDirty(config);
            }
        }

        // --- BIOMES --------------------------------------------------------------

        private void DrawBiomesGUI() {
            EditorGUILayout.LabelField(
                new GUIContent("Biomes", "Biome profiles referenced by the Object Brush. All are shown at once."),
                EditorStyles.boldLabel
            );

            scroll = EditorGUILayout.BeginScrollView(scroll);

            int removeIndex = -1;

            for (int i = 0; i < config.biomes.Count; i++) {
                ObjectBrushProfile biome = config.biomes[i];

                EditorGUILayout.BeginHorizontal();

                string title = biome != null ? biome.name : "(Missing biome)";
                bool expanded = GetBiomeExpanded(biome);
                expanded = EditorGUILayout.Foldout(expanded, title, true);
                SetBiomeExpanded(biome, expanded);

                if (GUILayout.Button(
                        new GUIContent("X", "Remove this biome from the brush (the asset is not deleted)."),
                        GUILayout.Width(24))) {
                    removeIndex = i;
                }

                EditorGUILayout.EndHorizontal();

                if (expanded && biome != null) {
                    EditorGUI.indentLevel++;
                    EnsureCategoryList(biome).DoLayoutList();
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space(2f);
            }

            EditorGUILayout.EndScrollView();

            if (removeIndex >= 0) {
                Undo.RecordObject(config, "Remove Biome");
                config.biomes.RemoveAt(removeIndex);
                EditorUtility.SetDirty(config);
            }

            EditorGUILayout.Space();
            DrawAddBiomeGUI();
        }

        private void DrawAddBiomeGUI() {
            EditorGUILayout.BeginHorizontal();

            biomeToAdd = (ObjectBrushProfile)EditorGUILayout.ObjectField(
                new GUIContent("Add Biome", "Assign a biome profile asset to reference it here."),
                biomeToAdd,
                typeof(ObjectBrushProfile),
                false
            );

            using (new EditorGUI.DisabledScope(biomeToAdd == null || config.biomes.Contains(biomeToAdd))) {
                if (GUILayout.Button(new GUIContent("Add", "Add the assigned biome to the list."),
                        GUILayout.Width(50))) {
                    Undo.RecordObject(config, "Add Biome");
                    config.biomes.Add(biomeToAdd);
                    EditorUtility.SetDirty(config);
                    biomeToAdd = null;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private bool GetBiomeExpanded(ObjectBrushProfile biome) {
            if (biome == null) {
                return false;
            }

            return !biomeExpanded.TryGetValue(biome, out bool value) || value;
        }

        private void SetBiomeExpanded(ObjectBrushProfile biome, bool value) {
            if (biome != null) {
                biomeExpanded[biome] = value;
            }
        }

        // --- CATEGORY LIST -------------------------------------------------------

        private ReorderableList EnsureCategoryList(ObjectBrushProfile biome) {
            if (categoryLists.TryGetValue(biome, out ReorderableList existing)) {
                existing.list = biome.categories;
                return existing;
            }

            ReorderableList list = new ReorderableList(
                biome.categories,
                typeof(ObjectBrushProfile.BiomeCategory),
                true, // draggable
                true, // header
                true, // add
                true // remove
            );

            list.drawHeaderCallback = rect => {
                EditorGUI.LabelField(rect, new GUIContent(
                    "Categories   (name   /   parent path under World)",
                    "Each category maps to a parent path relative to the World root, e.g. \"Interactive\" " +
                    "or \"Interactive/Barrels\". An empty path falls back to the category name."
                ));
            };

            list.elementHeight = EditorGUIUtility.singleLineHeight + 6f;

            list.drawElementCallback = (rect, index, isActive, isFocused) => {
                if (index < 0 || index >= biome.categories.Count) {
                    return;
                }

                ObjectBrushProfile.BiomeCategory cat = biome.categories[index];

                float padding = 3f;
                rect.y += padding;
                rect.height -= 2f * padding;

                float spacing = 6f;
                float nameWidth = rect.width * 0.4f;

                Rect nameRect = new Rect(rect.x, rect.y, nameWidth, EditorGUIUtility.singleLineHeight);
                Rect pathRect = new Rect(
                    nameRect.xMax + spacing,
                    rect.y,
                    rect.xMax - nameRect.xMax - spacing,
                    EditorGUIUtility.singleLineHeight
                );

                string pathControl = "obPath_" + biome.GetEntityId() + "_" + index;

                EditorGUI.BeginChangeCheck();

                string newName = EditorGUI.TextField(nameRect, cat.name);

                GUI.SetNextControlName(pathControl);
                string newPath = EditorGUI.TextField(pathRect, cat.parentPath);

                if (EditorGUI.EndChangeCheck()) {
                    Undo.RecordObject(biome, "Edit Category");
                    cat.name = newName;
                    cat.parentPath = newPath;
                    EditorUtility.SetDirty(biome);
                }

                // Faint hint showing the effective fallback path when no explicit path is set
                // and the field is not currently being edited.
                if (string.IsNullOrEmpty(cat.parentPath) && GUI.GetNameOfFocusedControl() != pathControl) {
                    Color old = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, 0.35f);
                    EditorGUI.LabelField(pathRect, " = " + cat.name);
                    GUI.color = old;
                }
            };

            list.onAddCallback = l => {
                Undo.RecordObject(biome, "Add Category");
                biome.categories.Add(new ObjectBrushProfile.BiomeCategory {
                    name = "Category " + biome.categories.Count
                });
                EditorUtility.SetDirty(biome);
            };

            list.onRemoveCallback = l => {
                if (l.index < 0 || l.index >= biome.categories.Count) {
                    return;
                }

                Undo.RecordObject(biome, "Remove Category");
                biome.categories.RemoveAt(l.index);
                EditorUtility.SetDirty(biome);
            };

            list.onReorderCallback = l => {
                EditorUtility.SetDirty(biome);
            };

            categoryLists[biome] = list;
            return list;
        }
    }
}
