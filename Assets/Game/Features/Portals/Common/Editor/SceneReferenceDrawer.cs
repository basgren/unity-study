#if UNITY_EDITOR
using Game.Core.Services.Scene;
using UnityEditor;
using UnityEngine;

namespace Game.Features.Portals.Common.Editor {
    /// <summary>
    /// Editor drawer that displays a SceneReference as a SceneAsset field. When the user changes
    /// the scene reference, all portal-kind caches are invalidated via <see cref="ScenePortalCacheInvalidator"/>
    /// so dependent dropdowns refresh immediately rather than waiting on TTL.
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
                ScenePortalCacheInvalidator.InvalidateAll();
            }

            EditorGUI.EndProperty();
        }
    }
}
#endif
