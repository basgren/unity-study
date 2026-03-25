#if UNITY_EDITOR
using Core.Models.Inventory;
using Game.Models;
using UnityEditor;
using UnityEngine;

namespace Core.Models.Editor {
    [CustomPropertyDrawer(typeof(ItemId))]
    public sealed class ItemIdDrawer : PropertyDrawer {
        private static string[] cachedIds;
        private static double lastCacheTime;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            var valueProp = property.FindPropertyRelative("value");
            if (valueProp == null) {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            var ids = GetIds();
            if (ids == null || ids.Length == 0) {
                EditorGUI.BeginProperty(position, label, property);
                valueProp.stringValue = EditorGUI.TextField(position, label, valueProp.stringValue);
                EditorGUI.EndProperty();
                return;
            }

            var current = valueProp.stringValue;
            var index = IndexOf(ids, current);

            // Show "<None>" when empty/invalid
            var display = new string[ids.Length + 1];
            display[0] = "<None>";
            for (var i = 0; i < ids.Length; i++) {
                display[i + 1] = ids[i];
            }

            var shownIndex = index >= 0 ? index + 1 : 0;

            EditorGUI.BeginProperty(position, label, property);
            var newShownIndex = EditorGUI.Popup(position, label.text, shownIndex, display);

            if (newShownIndex == 0) {
                valueProp.stringValue = string.Empty;
            } else {
                valueProp.stringValue = ids[newShownIndex - 1];
            }

            EditorGUI.EndProperty();
        }

        private static string[] GetIds() {
            // Cache for a short time to avoid heavy work every repaint.
            if (cachedIds != null && EditorApplication.timeSinceStartup - lastCacheTime < 1.0) {
                return cachedIds;
            }

            lastCacheTime = EditorApplication.timeSinceStartup;

            var facade = Resources.Load<DefsFacade>("DefsFacade");
            if (facade == null || facade.Items == null) {
                cachedIds = System.Array.Empty<string>();
                return cachedIds;
            }

            // You need a method that returns all ids from InventoryItemsDef.
            // See section 3 below for GetAllIds().
            cachedIds = facade.Items.GetAllIds();
            return cachedIds;
        }

        private static int IndexOf(string[] ids, string value) {
            if (string.IsNullOrEmpty(value)) {
                return -1;
            }

            for (var i = 0; i < ids.Length; i++) {
                if (ids[i] == value) {
                    return i;
                }
            }

            return -1;
        }
    }
}
#endif
