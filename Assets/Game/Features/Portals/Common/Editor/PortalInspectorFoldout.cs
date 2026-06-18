#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Game.Features.Portals.Common.Editor {
    /// <summary>
    /// Renders the "Kind ID: N" collapsed-by-default foldout with Copy ID and Change ID buttons.
    /// Door and Entrance editors both call this so the inspector UI stays identical.
    /// The <paramref name="applyChangeId"/> callback writes the new id through the kind-specific
    /// setter (Door.EditorSetDoorId / Entrance.EditorSetEntranceId).
    /// </summary>
    public static class PortalInspectorFoldout {
        public static void DrawIdFoldout(UnityEngine.Object portalObj, string kindName, string currentId,
            Action<string> applyChangeId) {
            var foldoutKey = $"PortalEditor.IdFoldout.{portalObj.GetEntityId()}";
            var wasOpen = SessionState.GetBool(foldoutKey, false);

            var displayId = string.IsNullOrEmpty(currentId) ? "<unassigned>" : currentId;
            var header = $"{kindName} ID: {displayId}";

            var nowOpen = EditorGUILayout.Foldout(wasOpen, header, true, EditorStyles.foldoutHeader);
            if (nowOpen != wasOpen) {
                SessionState.SetBool(foldoutKey, nowOpen);
            }

            if (!nowOpen) {
                return;
            }

            using (new EditorGUI.IndentLevelScope()) {
                using (new EditorGUILayout.HorizontalScope()) {
                    if (GUILayout.Button("Copy ID")) {
                        EditorGUIUtility.systemCopyBuffer = currentId;
                    }

                    if (GUILayout.Button("Change ID")) {
                        PortalChangeIdWindow.Show(portalObj, applyChangeId);
                    }
                }
            }
        }
    }
}
#endif
