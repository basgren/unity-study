#if UNITY_EDITOR
using Game.Features.Portals.Common.Editor;
using UnityEditor;

namespace Game.Features.Portals.Entrance.Editor {
    /// <summary>
    /// Entrance inspector. All id-foldout UI lives in the shared <see cref="PortalInspectorFoldout"/>;
    /// this editor only wires up the entrance-specific callback to write the new id back.
    /// </summary>
    [CustomEditor(typeof(Entrance))]
    public sealed class EntranceEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            var entrance = (Entrance)target;

            PortalInspectorFoldout.DrawIdFoldout(entrance, "Entrance", entrance.EntranceId, entrance.EditorSetEntranceId);

            EditorGUILayout.Space();

            DrawPropertiesExcluding(serializedObject, "m_Script", "entranceId");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
