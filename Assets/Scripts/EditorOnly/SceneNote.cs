using UnityEditor;
using UnityEngine;

namespace EditorOnly {
    [ExecuteAlways]
    public class SceneNote : MonoBehaviour {
        [TextArea(2, 6)]
        public string text = "Заметка…";

        public bool showTextInScene = true;

#if UNITY_EDITOR
        // private static Texture2D icon;

        private void OnDrawGizmos() {
            Gizmos.DrawIcon(transform.position, "console.warnicon", false);

            if (showTextInScene && !string.IsNullOrEmpty(text)) {
                GUIStyle style = new GUIStyle(EditorStyles.whiteLabel);
                style.normal.textColor = Color.yellow;
                style.fontStyle = FontStyle.Bold;

                Handles.Label(transform.position + (Vector3.right + Vector3.up) * 0.5f, text, style);
            }
        }
#endif

#if !UNITY_EDITOR
    private void Awake()
    {
        // В билде просто уничтожаем объект, чтобы заметки не светились
        Destroy(gameObject);
    }
#endif
    }
}
