using Game.UI.Widgets;
using UnityEditor;
using UnityEditor.UI;

namespace Core.UI.Widgets.Editor {
    
    [CustomEditor(typeof(MenuButton), true)]
    [CanEditMultipleObjects]
    public class MenuButtonEditor : ButtonEditor {
        public override void OnInspectorGUI() {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("style"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pressedTextOffset"));
            EditorGUILayout.Space();
            serializedObject.ApplyModifiedProperties();
            
            base.OnInspectorGUI();
        }
    }
}
