#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Features.Portals.Common.Editor {
    /// <summary>
    /// Single drawer for <see cref="PortalLink"/>. Picks the right portal kind from the owning
    /// component (Door, Entrance, ...) via <see cref="PortalKindRegistry"/> so the dropdown only
    /// lists portals of the matching kind.
    /// </summary>
    [CustomPropertyDrawer(typeof(PortalLink))]
    public sealed class PortalLinkDrawer : PropertyDrawer {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            return EditorGUIUtility.singleLineHeight * 2f + 2f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            var sceneProp = property.FindPropertyRelative("targetScene");
            var idProp = property.FindPropertyRelative("targetId");

            var lineH = EditorGUIUtility.singleLineHeight;
            var sceneRect = new Rect(position.x, position.y, position.width, lineH);
            var targetRect = new Rect(position.x, position.y + lineH + 2f, position.width, lineH);

            EditorGUI.BeginProperty(position, label, property);

            EditorGUI.PropertyField(sceneRect, sceneProp, new GUIContent("Target Scene"));

            var owner = property.serializedObject.targetObject;
            var kind = PortalKindRegistry.GetForComponent(owner);
            var fieldLabel = kind != null ? "Target " + kind.KindName : "Target";

            var guidProp = sceneProp.FindPropertyRelative("sceneGuid");
            var sceneGuid = guidProp != null ? guidProp.stringValue : string.Empty;

            if (string.IsNullOrWhiteSpace(sceneGuid)) {
                using (new EditorGUI.DisabledGroupScope(true)) {
                    EditorGUI.TextField(targetRect, fieldLabel, "<select scene first>");
                }

                EditorGUI.EndProperty();
                return;
            }

            // ScenePortalCache cannot OpenScene during play mode, so the dropdown would be empty
            // and falsely report "missing target". Show the stored id read-only instead.
            // The kind == null branch handles owners that aren't registered (defensive).
            if (Application.isPlaying || kind == null) {
                using (new EditorGUI.DisabledGroupScope(true)) {
                    var displayValue = string.IsNullOrEmpty(idProp.stringValue) ? "(None)" : idProp.stringValue;
                    EditorGUI.TextField(targetRect, fieldLabel, displayValue);
                }

                EditorGUI.EndProperty();
                return;
            }

            DrawPopup(targetRect, kind, owner, sceneGuid, idProp, fieldLabel);

            EditorGUI.EndProperty();
        }

        private static void DrawPopup(Rect targetRect, PortalKind kind, UnityEngine.Object owner, string sceneGuid,
            SerializedProperty idProp, string fieldLabel) {
            var ownerComponent = owner as Component;
            var ownerPortal = owner as IPortal;
            var ownerId = ownerPortal != null ? ownerPortal.Id : string.Empty;
            var ownerSceneGuid = ownerComponent != null && ownerComponent.gameObject != null
                ? PortalEditorUtils.GetSceneGuid(ownerComponent.gameObject.scene.path)
                : string.Empty;

            var entries = kind.Cache.GetEntriesByGuid(sceneGuid);
            var currentTargetId = idProp.stringValue;
            var names = new List<string>(entries.Length + 1) { "(None)" };
            var values = new List<string>(entries.Length + 1) { string.Empty };

            // Count name occurrences so we can disambiguate collisions with the hierarchy path.
            var nameCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < entries.Length; i++) {
                var n = entries[i].ObjectName ?? string.Empty;
                nameCounts.TryGetValue(n, out var c);
                nameCounts[n] = c + 1;
            }

            var selectedIndex = 0;
            for (var i = 0; i < entries.Length; i++) {
                var entryId = entries[i].Id;

                // Hide owner itself from the dropdown - self-links are forbidden.
                if (!string.IsNullOrWhiteSpace(ownerSceneGuid) &&
                    string.Equals(sceneGuid, ownerSceneGuid, StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(ownerId) &&
                    string.Equals(entryId, ownerId, StringComparison.Ordinal)) {
                    continue;
                }

                var objName = entries[i].ObjectName ?? string.Empty;
                var displayLabel = (nameCounts.TryGetValue(objName, out var count) && count > 1)
                    ? entries[i].HierarchyPath
                    : objName;

                names.Add(displayLabel);
                values.Add(entryId);

                if (!string.IsNullOrWhiteSpace(currentTargetId) &&
                    string.Equals(currentTargetId, entryId, StringComparison.Ordinal)) {
                    selectedIndex = values.Count - 1;
                }
            }

            var showMissingWarning = !string.IsNullOrWhiteSpace(currentTargetId) && selectedIndex == 0;
            var popupRect = targetRect;
            const float WarnWidth = 200f;

            if (showMissingWarning) {
                popupRect.width = Mathf.Max(0f, targetRect.width - WarnWidth);
            }

            var newIndex = EditorGUI.Popup(popupRect, fieldLabel, selectedIndex, names.ToArray());

            // Only write back when the USER changed the selection. Popup returns the passed-in
            // selectedIndex when nothing was clicked this frame; comparing values vs currentTargetId
            // would clear the saved id whenever the scene cache returns empty (e.g. during play-mode
            // entry), because selectedIndex stays at 0 ("None").
            if (newIndex != selectedIndex) {
                idProp.stringValue = values[newIndex];
                idProp.serializedObject.ApplyModifiedProperties();
            }

            if (showMissingWarning) {
                var warnRect = new Rect(targetRect.xMax - WarnWidth, targetRect.y, WarnWidth, targetRect.height);
                EditorGUI.LabelField(warnRect, "Missing target " + kind.KindName.ToLowerInvariant(),
                    EditorStyles.miniBoldLabel);
            }
        }
    }
}
#endif
