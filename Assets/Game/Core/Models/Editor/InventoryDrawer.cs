#if UNITY_EDITOR
using Core.Models;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Models.Editor {
    [CustomPropertyDrawer(typeof(global::Game.Core.Models.Inventory.InventoryModel))]
    public sealed class InventoryDrawer : PropertyDrawer {
        private const float PaddingY = 2f;
        private const float ColumnGap = 8f;
        private const float CountWidth = 70f;
        
        private GUIStyle lightLabel;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            var itemsProp = property.FindPropertyRelative("items");
            var count = itemsProp != null ? itemsProp.arraySize : 0;

            if (!property.isExpanded) {
                return EditorGUIUtility.singleLineHeight;
            }

            // header row + (at least 1 row)
            var rows = Mathf.Max(1, count + 1);
            return EditorGUIUtility.singleLineHeight + PaddingY + rows * (EditorGUIUtility.singleLineHeight + PaddingY);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            var itemsProp = property.FindPropertyRelative("items");

            var headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, label, true);

            if (!property.isExpanded) {
                return;
            }
            
            if (lightLabel == null) {
                lightLabel = new GUIStyle(EditorStyles.label);
                lightLabel.normal.textColor = new Color(1f, 1f, 1f, 1f); // a bit brighter
            }

            EditorGUI.indentLevel++;

            var y = position.y + EditorGUIUtility.singleLineHeight + PaddingY;

            EditorGUI.BeginDisabledGroup(true);

            if (itemsProp == null) {
                EditorGUI.LabelField(new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight), "No 'items' field found");
                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
                return;
            }

            var idRect = new Rect(position.x, y, position.width - CountWidth - ColumnGap, EditorGUIUtility.singleLineHeight);
            var countRect = new Rect(position.x + position.width - CountWidth, y, CountWidth, EditorGUIUtility.singleLineHeight);

            // Header row
            EditorGUI.LabelField(idRect, "Item");
            EditorGUI.LabelField(countRect, "Count");
            y += EditorGUIUtility.singleLineHeight + PaddingY;

            if (itemsProp.arraySize == 0) {
                idRect.y = y;
                EditorGUI.LabelField(idRect, "<empty>");
                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
                return;
            }

            for (var i = 0; i < itemsProp.arraySize; i++) {
                var element = itemsProp.GetArrayElementAtIndex(i);

                var idProp = element.FindPropertyRelative("id");     // ItemId
                var countProp = element.FindPropertyRelative("count"); // int

                idRect.y = y;
                countRect.y = y;

                var idText = GetItemIdDisplay(idProp);
                var countText = countProp != null ? countProp.intValue.ToString() : "0";

                EditorGUI.LabelField(idRect, idText, lightLabel);
                EditorGUI.LabelField(countRect, countText, lightLabel);

                y += EditorGUIUtility.singleLineHeight + PaddingY;
            }

            EditorGUI.EndDisabledGroup();
            EditorGUI.indentLevel--;
        }

        private static string GetItemIdDisplay(SerializedProperty idProp) {
            if (idProp == null) {
                return "<no id>";
            }

            // ItemId is a struct with serialized field "value" (as we used before)
            // If yours is named differently, change "value".
            var valueProp = idProp.FindPropertyRelative("value");
            var id = valueProp != null ? valueProp.stringValue : string.Empty;

            if (string.IsNullOrEmpty(id)) {
                return "<None>";
            }

            // Optional: show friendly info from defs if available
            // Right now your ItemDef only has id, so it will show the same.
            var def = DefsFacade.I.Items.Get(id);
            if (def != null && !def.IsEmpty) {
                return def.Id;
            }

            return id + " (missing def)";
        }
    }
}
#endif
