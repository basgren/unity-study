#if UNITY_EDITOR
using Game.Features.Portals.Common.Editor;
using UnityEditor;

namespace Game.Features.Portals.Portal.Editor {
    /// <summary>
    /// Portal inspector. The id foldout is the shared <see cref="PortalInspectorFoldout"/>; this
    /// editor wires the Portal-specific setter and then draws the remaining serialized fields
    /// (including the PortalLink, which gets the shared dropdown drawer).
    /// </summary>
    [CustomEditor(typeof(Portal))]
    public sealed class PortalEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            var portal = (Portal)target;

            PortalInspectorFoldout.DrawIdFoldout(portal, "Portal", portal.PortalId, portal.EditorSetPortalId);

            EditorGUILayout.Space();

            DrawPropertiesExcluding(serializedObject, "m_Script", "portalId");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
