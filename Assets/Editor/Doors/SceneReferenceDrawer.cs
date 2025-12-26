#if UNITY_EDITOR
using Doors;
using UnityEditor;
using UnityEngine;

namespace Editor.Doors {
    /// <summary>
    /// Editor drawer that displays a SceneReference as a SceneAsset field.
    /// </summary>
    [CustomPropertyDrawer(typeof(SceneReference))]
    public sealed class SceneReferenceDrawer : PropertyDrawer {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            var guidProp = property.FindPropertyRelative("sceneGuid");
            var pathProp = property.FindPropertyRelative("scenePath");

            var currentGuid = guidProp.stringValue;
            SceneAsset currentAsset = null;

            if (!string.IsNullOrWhiteSpace(currentGuid)) {
                var path = AssetDatabase.GUIDToAssetPath(currentGuid);
                if (!string.IsNullOrWhiteSpace(path)) {
                    currentAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                }
            }

            EditorGUI.BeginProperty(position, label, property);

            var newAsset = (SceneAsset)EditorGUI.ObjectField(position, label, currentAsset, typeof(SceneAsset), false);
            if (newAsset != currentAsset) {
                if (newAsset == null) {
                    guidProp.stringValue = string.Empty;
                    pathProp.stringValue = string.Empty;
                } else {
                    var newPath = AssetDatabase.GetAssetPath(newAsset);
                    var newGuid = AssetDatabase.AssetPathToGUID(newPath);

                    guidProp.stringValue = newGuid;
                    pathProp.stringValue = newPath;
                }

                property.serializedObject.ApplyModifiedProperties();
                SceneDoorCache.InvalidateAll();
            }

            EditorGUI.EndProperty();
        }
    }
}
#endif
