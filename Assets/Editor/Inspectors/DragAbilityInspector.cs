using Components.Abilities;
using UnityEditor;
using UnityEngine;

namespace Editor.Inspectors {
    [CustomEditor(typeof(DraggableBarrel))]
    public class DraggableBarrelInspector : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            if (Application.isPlaying) {
                var comp = (DraggableBarrel)target;

                if (comp == null) {
                    return;
                }

                EditorGUILayout.LabelField("Joint With Upper Body", EditorStyles.boldLabel);
                
                EditorGUILayout.LabelField(
                    "Reactive Force On Break", 
                    comp.ReactiveForceOnBreak >= 0
                        ? comp.ReactiveForceOnBreak.ToString()
                        : EditorConst.NA
                );

                EditorGUILayout.LabelField(
                    "Reactive Torque On Break",
                    comp.ReactiveTorqueOnBreak >= 0
                        ? comp.ReactiveTorqueOnBreak.ToString()
                        : EditorConst.NA
                    );

                EditorGUILayout.Space();
            }

            DrawDefaultInspector();
        }
    }
}
