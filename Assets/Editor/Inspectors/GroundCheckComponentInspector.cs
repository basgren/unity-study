using Components.Collisions;
using UnityEditor;
using UnityEngine;

namespace Editor.Inspectors {
    [CustomEditor(typeof(GroundCheckComponent))]
    public class GroundCheckComponentInspector: UnityEditor.Editor {
        public override void OnInspectorGUI() {
            if (Application.isPlaying) {
                var component = (GroundCheckComponent)target;

                if (component != null) {
                    EditorGUILayout.LabelField("IsGrounded", component.IsGrounded.ToString());
                }
            }
            
            DrawDefaultInspector();
        }
    }
}
