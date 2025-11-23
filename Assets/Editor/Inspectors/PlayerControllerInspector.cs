using PixelCrew.Player;
using UnityEditor;
using UnityEngine;

namespace Editor.Inspectors {
    [CustomEditor(typeof(PlayerController))]
    public class PlayerControllerInspector: UnityEditor.Editor {
        public override void OnInspectorGUI() {
            if (Application.isPlaying) {
                var player = (PlayerController)target;

                EditorGUILayout.LabelField("Input.Move", player.Actions.Move.ReadValue<Vector2>().ToString());
                EditorGUILayout.Space();
                
                EditorGUILayout.LabelField("isGrounded", player.IsGrounded.ToString());
                EditorGUILayout.Space();
            }
            
            DrawDefaultInspector();
        }
    }
}
