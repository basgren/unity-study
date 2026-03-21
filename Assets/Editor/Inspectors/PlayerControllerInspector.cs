using Game.Player;
using UnityEditor;
using UnityEngine;

namespace Editor.Inspectors {
    [CustomEditor(typeof(PlayerController))]
    public class PlayerControllerInspector : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            if (Application.isPlaying) {
                var player = (PlayerController)target;

                if (player != null) {
                    EditorGUILayout.LabelField("Input.Move", GetMoveText(player));
                    EditorGUILayout.Space();

                    EditorGUILayout.LabelField("isGrounded", player.IsGrounded.ToString());
                    EditorGUILayout.Space();
                }
            }

            DrawDefaultInspector();
        }

        private static string GetMoveText(PlayerController player) {
            try {
                var moveAction = player.Actions.Move;
                return moveAction != null
                    ? moveAction.ReadValue<Vector2>().ToString()
                    : "<not initialized>";
            } catch {
                return "<not initialized>";
            }
        }
    }
}
