using Prefabs.Characters.Sharky;
using UnityEditor;
using UnityEngine;

namespace Editor.Inspectors {
    [CustomEditor(typeof(SharkyController))]
    public class SharkyControllerInspector: UnityEditor.Editor {
        public override void OnInspectorGUI() {
            if (Application.isPlaying) {
                var comp = (SharkyController)target;

                if (comp != null) {
                    EditorGUILayout.LabelField("isGrounded", comp.IsGrounded.ToString());
                    EditorGUILayout.Space();
                }
            }
            
            DrawDefaultInspector();
        }
    }
}
