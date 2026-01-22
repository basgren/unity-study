using Core.Components;
using Core.Components.Animation;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Editor {
    [CustomEditor(typeof(MultiStateSpriteAnimator))]
    public sealed class MultiStateSpriteAnimatorEditor : UnityEditor.Editor {
        private const float DragHandleWidth = 16f;
        
        private SerializedProperty clipsProp;
        private ReorderableList clipsList;

        private void OnEnable() {
            clipsProp = serializedObject.FindProperty("clips");

            clipsList = new ReorderableList(serializedObject, clipsProp, true, true, true, true);

            clipsList.drawHeaderCallback = rect => { EditorGUI.LabelField(rect, "Clips (drag to reorder)"); };

            clipsList.drawElementCallback = (rect, index, isActive, isFocused) => {
                var element = clipsProp.GetArrayElementAtIndex(index);

                var nameProp = element.FindPropertyRelative("name");
                var clipName = nameProp != null ? nameProp.stringValue : string.Empty;

                if (string.IsNullOrWhiteSpace(clipName)) {
                    clipName = $"Clip {index} (no name)";
                }

                // Reserve space for the drag handle so foldout doesn't overlap it.
                rect.xMin += DragHandleWidth;
                rect.y += 2f;
                
                rect.height = EditorGUI.GetPropertyHeight(element, true);

                EditorGUI.PropertyField(rect, element, new GUIContent(clipName), true);
            };

            clipsList.elementHeightCallback = index => {
                var element = clipsProp.GetArrayElementAtIndex(index);
                return EditorGUI.GetPropertyHeight(element, true) + 6f;
            };
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();

            // Draw everything except clips with default inspector
            DrawPropertiesExcluding(serializedObject, "clips");

            EditorGUILayout.Space(6f);
            clipsList.DoLayoutList();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
