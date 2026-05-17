#if UNITY_EDITOR
using Game.Features.Portals.Common.Editor;
using UnityEditor;

namespace Game.Features.Portals.Doors.Editor {
    /// <summary>
    /// Door inspector. All id-foldout UI lives in the shared <see cref="PortalInspectorFoldout"/>;
    /// this editor only wires up the door-specific callback to write the new id back.
    /// </summary>
    [CustomEditor(typeof(Door))]
    public sealed class DoorEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            var door = (Door)target;

            PortalInspectorFoldout.DrawIdFoldout(door, "Door", door.DoorId, door.EditorSetDoorId);

            EditorGUILayout.Space();

            DrawPropertiesExcluding(serializedObject, "m_Script", "doorId");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
