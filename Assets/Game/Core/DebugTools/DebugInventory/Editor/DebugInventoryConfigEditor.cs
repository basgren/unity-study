#if UNITY_EDITOR
using System.Collections.Generic;
using Core.Models;
using Game.Configs;
using Game.Core.Bootstrap;
using Game.Core.Models.Inventory;
using UnityEditor;
using UnityEngine;

namespace Game.Core.DebugTools.Editor {
    /// <summary>
    /// Inspector for <see cref="DebugInventoryConfig"/>. In edit mode it shows and edits the
    /// serialized initial-items list (seeded at game start). In play mode it shows and edits the
    /// live player inventory instead, so pickups appear immediately and edits act like pickups/drops.
    /// </summary>
    [CustomEditor(typeof(DebugInventoryConfig))]
    public sealed class DebugInventoryConfigEditor : UnityEditor.Editor {
        private const float IconSize = 28f;
        private const string MainConfigResource = "MainConfig";

        private int addSelectedIndex;
        private int addCount = 1;

        // Live play-mode inventory changes every frame; keep the inspector in sync.
        public override bool RequiresConstantRepaint() {
            return Application.isPlaying;
        }

        public override void OnInspectorGUI() {
            DrawHintBox();
            EditorGUILayout.Space();

            if (Application.isPlaying) {
                DrawPlayModeInventory();
            } else {
                DrawEditModeInventory();
            }

            EditorGUILayout.Space();
            DrawAddRow();
        }

        private void DrawHintBox() {
            EditorGUILayout.HelpBox(
                "Edit mode: this list seeds the player inventory when the game starts.\n" +
                "Play mode: shows and edits the live player inventory (changes act like pickups/drops).",
                MessageType.None);

            var config = Resources.Load<MainConfig>(MainConfigResource);
            var enabled = config != null && config.DebugSystems != null && config.DebugSystems.EnableDebugInventory;

            using (new EditorGUILayout.HorizontalScope()) {
                if (enabled) {
                    EditorGUILayout.HelpBox("Status: ENABLED via MainConfig → Debug Systems.", MessageType.Info);
                } else {
                    EditorGUILayout.HelpBox(
                        "Status: DISABLED. Enable it in MainConfig → Debug Systems → Enable Debug Inventory.",
                        MessageType.Warning);
                }

                if (config != null && GUILayout.Button("Ping\nMainConfig", GUILayout.Width(90), GUILayout.Height(38))) {
                    Selection.activeObject = config;
                    EditorGUIUtility.PingObject(config);
                }
            }
        }

        private void DrawEditModeInventory() {
            serializedObject.Update();
            var itemsProp = serializedObject.FindProperty("initialItems");

            EditorGUILayout.LabelField("Initial Items", EditorStyles.boldLabel);

            if (itemsProp.arraySize == 0) {
                EditorGUILayout.LabelField("  <empty>");
            }

            var removeIndex = -1;
            var clearAll = false;

            for (var i = 0; i < itemsProp.arraySize; i++) {
                var element = itemsProp.GetArrayElementAtIndex(i);
                var idProp = element.FindPropertyRelative("itemId").FindPropertyRelative("value");
                var countProp = element.FindPropertyRelative("count");
                var id = idProp != null ? idProp.stringValue : string.Empty;
                var count = countProp != null ? countProp.intValue : 0;

                if (DrawItemRow(id, count)) {
                    removeIndex = i;
                }
            }

            if (itemsProp.arraySize > 0 && GUILayout.Button("Clear All")) {
                clearAll = true;
            }

            if (clearAll) {
                itemsProp.ClearArray();
            } else if (removeIndex >= 0) {
                itemsProp.DeleteArrayElementAtIndex(removeIndex);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPlayModeInventory() {
            EditorGUILayout.LabelField("Live Player Inventory", EditorStyles.boldLabel);

            var inventory = GetLiveInventory();
            if (inventory == null) {
                EditorGUILayout.HelpBox("Live inventory not available yet (player state not initialized).", MessageType.Info);
                return;
            }

            var items = inventory.Items;
            if (items.Count == 0) {
                EditorGUILayout.LabelField("  <empty>");
            }

            ItemId? removeId = null;
            var clearAll = false;

            for (var i = 0; i < items.Count; i++) {
                var item = items[i];
                if (DrawItemRow(item.id, item.count)) {
                    removeId = item.id;
                }
            }

            if (items.Count > 0 && GUILayout.Button("Clear All")) {
                clearAll = true;
            }

            // Apply mutations after drawing so the items list is not modified mid-iteration.
            if (clearAll) {
                var ids = new List<ItemId>(items.Count);
                foreach (var item in items) {
                    ids.Add(item.id);
                }

                foreach (var id in ids) {
                    inventory.Remove(id, inventory.GetCount(id));
                }
            } else if (removeId.HasValue) {
                inventory.Remove(removeId.Value, inventory.GetCount(removeId.Value));
            }
        }

        private void DrawAddRow() {
            EditorGUILayout.LabelField("Add Item", EditorStyles.boldLabel);

            var ids = GetItemIds();
            if (ids.Length == 0) {
                EditorGUILayout.HelpBox("No items defined in DefsFacade.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope()) {
                addSelectedIndex = Mathf.Clamp(addSelectedIndex, 0, ids.Length - 1);
                addSelectedIndex = EditorGUILayout.Popup(addSelectedIndex, ids);
                addCount = Mathf.Max(1, EditorGUILayout.IntField(addCount, GUILayout.Width(60)));

                if (GUILayout.Button("Add", GUILayout.Width(60))) {
                    AddItem(ids[addSelectedIndex], addCount);
                }
            }
        }

        private bool DrawItemRow(string id, int count) {
            var remove = false;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox)) {
                var iconRect = GUILayoutUtility.GetRect(IconSize, IconSize, GUILayout.Width(IconSize), GUILayout.Height(IconSize));
                var def = string.IsNullOrEmpty(id) ? null : DefsFacade.I.Items.Get(id);
                DrawSprite(iconRect, def != null ? def.Icon : null);

                var name = string.IsNullOrEmpty(id) ? "<None>" : id;
                if (def == null && !string.IsNullOrEmpty(id)) {
                    name = id + " (missing def)";
                }

                EditorGUILayout.LabelField(name);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("x" + count, GUILayout.Width(50));

                if (GUILayout.Button("X", GUILayout.Width(24))) {
                    remove = true;
                }
            }

            return remove;
        }

        private void AddItem(string id, int count) {
            if (string.IsNullOrEmpty(id) || count <= 0) {
                return;
            }

            if (Application.isPlaying) {
                var inventory = GetLiveInventory();
                if (inventory != null) {
                    inventory.Add(id, count);
                }

                return;
            }

            // Edit mode: stack into the serialized initial-items list (mirrors pickup behavior).
            serializedObject.Update();
            var itemsProp = serializedObject.FindProperty("initialItems");

            for (var i = 0; i < itemsProp.arraySize; i++) {
                var element = itemsProp.GetArrayElementAtIndex(i);
                var idProp = element.FindPropertyRelative("itemId").FindPropertyRelative("value");
                if (idProp != null && idProp.stringValue == id) {
                    var countProp = element.FindPropertyRelative("count");
                    countProp.intValue += count;
                    serializedObject.ApplyModifiedProperties();
                    return;
                }
            }

            itemsProp.arraySize++;
            var newElement = itemsProp.GetArrayElementAtIndex(itemsProp.arraySize - 1);
            newElement.FindPropertyRelative("itemId").FindPropertyRelative("value").stringValue = id;
            newElement.FindPropertyRelative("count").intValue = count;
            serializedObject.ApplyModifiedProperties();
        }

        private static InventoryModel GetLiveInventory() {
            var playerState = G.Game != null ? G.Game.playerState : null;
            return playerState != null ? playerState.InventoryModel : null;
        }

        private static string[] GetItemIds() {
            var facade = DefsFacade.I;
            if (facade == null || facade.Items == null) {
                return System.Array.Empty<string>();
            }

            return facade.Items.GetAllIds();
        }

        private static void DrawSprite(Rect rect, Sprite sprite) {
            // Faint backing box so empty/missing icons still read as a slot.
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.1f));

            if (sprite == null || sprite.texture == null) {
                return;
            }

            // Draw only the sprite's region of its texture (atlas-safe), preserving pixel art.
            var tex = sprite.texture;
            var tr = sprite.textureRect;
            var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
            GUI.DrawTextureWithTexCoords(rect, tex, uv);
        }
    }
}
#endif
