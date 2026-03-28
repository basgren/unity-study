#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core.Models.Inventory;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Game.Core.Models.Editor {
    [CustomEditor(typeof(InventoryItemsDef))]
    public sealed class InventoryItemsDefEditor : UnityEditor.Editor {
        private const string FoldoutKey = "InventoryItemsDefEditor.CSharpGenerationFoldout";
        private bool showGeneration;

        private SerializedProperty itemsProp;
        private ReorderableList list;

        private SerializedProperty generateCSharpClassProp;
        private SerializedProperty csharpClassFileProp;
        private SerializedProperty csharpClassNameProp;
        private SerializedProperty csharpClassNamespaceProp;

        private HashSet<string> duplicateIds = new HashSet<string>(StringComparer.Ordinal);
        private int emptyIdCount;

        private void OnEnable() {
            showGeneration = EditorPrefs.GetBool(FoldoutKey, true);

            itemsProp = serializedObject.FindProperty("items");

            generateCSharpClassProp = serializedObject.FindProperty("generateCSharpClass");
            csharpClassFileProp = serializedObject.FindProperty("csharpClassFile");
            csharpClassNameProp = serializedObject.FindProperty("csharpClassName");
            csharpClassNamespaceProp = serializedObject.FindProperty("csharpClassNamespace");

            list = new ReorderableList(serializedObject, itemsProp, true, true, true, true);

            list.drawHeaderCallback = rect => { EditorGUI.LabelField(rect, "Inventory Items"); };

            const float iconSize = 48f;
            const float padding = 2f;
            const float lineSpacing = 2f;

            list.elementHeight = iconSize + padding * 2f;

            list.drawElementCallback = (rect, index, isActive, isFocused) => {
                var element = itemsProp.GetArrayElementAtIndex(index);

                var idProp = element.FindPropertyRelative("id");
                var uidProp = element.FindPropertyRelative("uid");
                var iconProp = element.FindPropertyRelative("icon");
                var typeProp = element.FindPropertyRelative("type");

                rect.y += padding;

                // Ensure uid exists
                if (uidProp != null && string.IsNullOrEmpty(uidProp.stringValue)) {
                    uidProp.stringValue = Guid.NewGuid().ToString("N");
                }

                // Icon preview on the left
                var iconRect = new Rect(rect.x, rect.y, iconSize, iconSize);
                if (iconProp != null) {
                    iconProp.objectReferenceValue = EditorGUI.ObjectField(
                        iconRect, iconProp.objectReferenceValue, typeof(Sprite), false);
                }

                // Fields to the right of the icon
                var fieldsX = rect.x + iconSize + 6f;
                var fieldsWidth = rect.width - iconSize - 6f;
                var lineHeight = EditorGUIUtility.singleLineHeight;

                var prevLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 36f;

                // Id field
                var idRect = new Rect(fieldsX, rect.y, fieldsWidth, lineHeight);

                var id = idProp != null ? idProp.stringValue : string.Empty;
                var isEmpty = string.IsNullOrWhiteSpace(id);
                var isDuplicate = !isEmpty && duplicateIds.Contains(id);

                var controlName = uidProp != null ? $"ItemId_{uidProp.stringValue}" : $"ItemId_{index}";
                GUI.SetNextControlName(controlName);

                EditorGUI.BeginChangeCheck();
                var newValue = EditorGUI.DelayedTextField(idRect, "Id", id);
                if (EditorGUI.EndChangeCheck()) {
                    if (idProp != null) {
                        idProp.stringValue = newValue;
                    }
                }

                if (isEmpty) {
                    DrawOutline(idRect, new Color(1f, 0.6f, 0.2f, 0.9f));
                } else if (isDuplicate) {
                    DrawOutline(idRect, new Color(1f, 0.2f, 0.2f, 0.9f));
                }

                // Type field
                var typeRect = new Rect(fieldsX, idRect.yMax + lineSpacing, fieldsWidth, lineHeight);
                if (typeProp != null) {
                    EditorGUI.PropertyField(typeRect, typeProp, new GUIContent("Type"));
                }

                EditorGUIUtility.labelWidth = prevLabelWidth;
            };

            list.onAddCallback = l => {
                EndTextEditing();

                itemsProp.arraySize++;
                var i = itemsProp.arraySize - 1;

                var element = itemsProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("id").stringValue = string.Empty;
                element.FindPropertyRelative("uid").stringValue = Guid.NewGuid().ToString("N");
                element.FindPropertyRelative("icon").objectReferenceValue = null;
                element.FindPropertyRelative("type").enumValueIndex = 0;

                serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            };
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();

            DrawCodeGenerationUi();
            
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            RecalculateValidation();
            DrawToolbar();

            if (emptyIdCount > 0 || duplicateIds.Count > 0) {
                var parts = new List<string>();

                if (emptyIdCount > 0) {
                    parts.Add($"Empty ids: {emptyIdCount}");
                }

                if (duplicateIds.Count > 0) {
                    parts.Add($"Duplicate ids: {duplicateIds.Count}");
                }

                EditorGUILayout.HelpBox(string.Join(" | ", parts), MessageType.Warning);
            }

            list.DoLayoutList();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawCodeGenerationUi() {
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            showGeneration = EditorGUILayout.Foldout(showGeneration, "C# Class Generation", true);
            if (EditorGUI.EndChangeCheck()) {
                EditorPrefs.SetBool(FoldoutKey, showGeneration);
            }

            if (!showGeneration) {
                return;
            }

            using (new EditorGUI.IndentLevelScope()) {
                EditorGUILayout.PropertyField(generateCSharpClassProp, new GUIContent("Generate C# Class"));

                using (new EditorGUI.DisabledScope(!generateCSharpClassProp.boolValue)) {
                    DrawClassFileRow();
                    EditorGUILayout.PropertyField(csharpClassNameProp, new GUIContent("C# Class Name"));
                    EditorGUILayout.PropertyField(csharpClassNamespaceProp, new GUIContent("C# Class Namespace"));

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Generate now", GUILayout.Width(120f))) {
                        serializedObject.ApplyModifiedProperties();
                        ItemIdsGenerator.GenerateFor((InventoryItemsDef)target);
                        GUIUtility.ExitGUI();
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space();
        }

        private void DrawClassFileRow() {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.PrefixLabel("C# Class File");
            EditorGUILayout.SelectableLabel(csharpClassFileProp.stringValue ?? string.Empty,
                EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

            if (GUILayout.Button("Browse...", GUILayout.Width(80f))) {
                var def = (InventoryItemsDef)target;

                var current = csharpClassFileProp.stringValue ?? string.Empty;
                var defaultFolder = "Assets";
                var defaultName = "ItemIds.cs";

                if (!string.IsNullOrEmpty(current)) {
                    defaultFolder = Path.GetDirectoryName(current)?.Replace('\\', '/');
                    defaultName = Path.GetFileName(current);
                } else {
                    var assetPath = AssetDatabase.GetAssetPath(def);
                    if (!string.IsNullOrEmpty(assetPath)) {
                        defaultFolder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
                    }
                }

                var chosen = EditorUtility.SaveFilePanelInProject(
                    "Select C# class file",
                    defaultName,
                    "cs",
                    "Choose where to generate the class file",
                    defaultFolder);

                if (!string.IsNullOrEmpty(chosen)) {
                    // Prevent generating into Editor folder
                    if (chosen.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0) {
                        EditorUtility.DisplayDialog(
                            "Invalid path",
                            "Do not generate into an Editor folder. Pick a runtime folder under Assets/.",
                            "OK");
                    } else {
                        csharpClassFileProp.stringValue = chosen;

                        // If class name is empty, derive from file name
                        if (string.IsNullOrWhiteSpace(csharpClassNameProp.stringValue)) {
                            var fileNoExt = Path.GetFileNameWithoutExtension(chosen);
                            csharpClassNameProp.stringValue = fileNoExt;
                        }
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar() {
            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(itemsProp.arraySize <= 1)) {
                if (GUILayout.Button("Sort by Id", GUILayout.Width(110f))) {
                    // Robust: end editing before reordering
                    EndTextEditing();
                    SortById();
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }
            }

            if (GUILayout.Button("Validate", GUILayout.Width(80f))) {
                RecalculateValidation();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void EndTextEditing() {
            // Force IMGUI to stop editing the current TextField
            EditorGUIUtility.editingTextField = false;

            GUI.FocusControl(null);
            GUIUtility.keyboardControl = 0;
            GUIUtility.hotControl = 0;

            serializedObject.ApplyModifiedProperties();
        }

        private void RecalculateValidation() {
            duplicateIds.Clear();
            emptyIdCount = 0;

            var counts = new Dictionary<string, int>(StringComparer.Ordinal);

            for (var i = 0; i < itemsProp.arraySize; i++) {
                var element = itemsProp.GetArrayElementAtIndex(i);
                var idProp = element.FindPropertyRelative("id");
                var id = idProp != null ? idProp.stringValue : string.Empty;

                if (string.IsNullOrWhiteSpace(id)) {
                    emptyIdCount++;
                    continue;
                }

                if (counts.TryGetValue(id, out var c)) {
                    counts[id] = c + 1;
                } else {
                    counts.Add(id, 1);
                }
            }

            foreach (var kv in counts) {
                if (kv.Value > 1) {
                    duplicateIds.Add(kv.Key);
                }
            }
        }

        private void SortById() {
            var n = itemsProp.arraySize;
            var entries = new List<Entry>(n);

            for (var i = 0; i < n; i++) {
                var element = itemsProp.GetArrayElementAtIndex(i);
                var idProp = element.FindPropertyRelative("id");
                var uidProp = element.FindPropertyRelative("uid");

                var id = idProp != null ? (idProp.stringValue ?? string.Empty) : string.Empty;
                var uid = uidProp != null ? (uidProp.stringValue ?? string.Empty) : string.Empty;

                entries.Add(new Entry(i, id, uid));
            }

            entries.Sort((a, b) => {
                var c = StringComparer.Ordinal.Compare(a.Id, b.Id);
                if (c != 0) {
                    return c;
                }

                return StringComparer.Ordinal.Compare(a.Uid, b.Uid);
            });

            var p = entries.Select(e => e.OriginalIndex).ToArray();
            var pos = new int[n];
            for (var i = 0; i < n; i++) {
                pos[i] = i;
            }

            for (var target = 0; target < n; target++) {
                var orig = p[target];
                var current = pos[orig];

                if (current == target) {
                    continue;
                }

                itemsProp.MoveArrayElement(current, target);
                UpdatePositionsAfterMove(pos, current, target, orig);
            }
        }

        private static void UpdatePositionsAfterMove(int[] pos, int from, int to, int movedOrig) {
            if (from < to) {
                for (var i = 0; i < pos.Length; i++) {
                    var p = pos[i];
                    if (p > from && p <= to) {
                        pos[i] = p - 1;
                    }
                }
            } else {
                for (var i = 0; i < pos.Length; i++) {
                    var p = pos[i];
                    if (p >= to && p < from) {
                        pos[i] = p + 1;
                    }
                }
            }

            pos[movedOrig] = to;
        }

        private static void DrawOutline(Rect rect, Color color) {
            var fill = new Color(0f, 0f, 0f, 0f);
            Handles.DrawSolidRectangleWithOutline(rect, fill, color);
        }

        private readonly struct Entry {
            public readonly int OriginalIndex;
            public readonly string Id;
            public readonly string Uid;

            public Entry(int originalIndex, string id, string uid) {
                OriginalIndex = originalIndex;
                Id = id;
                Uid = uid;
            }
        }
    }
}
#endif
