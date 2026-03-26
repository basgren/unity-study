using System;
using Core.Components.Interaction;
using Game.Doors;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Features.Doors {
    /// <summary>
    /// A scene door that can transition the player to another scene/door.
    /// The door uses a stable string ID (DoorId) that is referenced by other doors.
    /// </summary>
    public sealed class Door : InteractableBase {
        [SerializeField, HideInInspector]
        private string doorId;

        [SerializeField]
        private DoorLink link;

        [SerializeField]
        private Transform entryPoint;

        [SerializeField]
        private UnityEvent onEntered;

        /// <summary>
        /// Door identifier. Must be unique within a scene.
        /// Not editable directly; use the editor "Change ID" action to rename safely.
        /// </summary>
        public string DoorId => doorId;

        /// <summary>
        /// Destination link for this door.
        /// </summary>
        public DoorLink Link => link;

        public Vector3 GetEntryPosition() {
            if (entryPoint != null) {
                return entryPoint.position;
            }

            return transform.position;
        }

        /// <summary>
        /// Immediately loads target scene and teleports player there.
        /// </summary>
        public void TravelToTarget() {
            DoorTravelService.Travel(this);
        }

        /// <summary>
        /// Call this method from outer handlers when player has entered the door (close animation will be played).
        /// </summary>
        public void NotifyEntered() {
            onEntered?.Invoke();
        }

        private void OnDrawGizmos() {
            if (entryPoint == null) {
                return;
            }

            if (string.IsNullOrWhiteSpace(link.TargetDoorId)) {
                return;
            }

            Gizmos.color = new Color(0, 0, 1, 0.7f);
            var pos = entryPoint.transform.position;

            Gizmos.DrawSphere(pos, 0.1f);

            if (link.TargetScene.ScenePath == gameObject.scene.path) {
                var targetDoor = DoorUtils.FindDoorByIdInScene(gameObject.scene, link.TargetDoorId);
                Gizmos.DrawLine(pos, targetDoor.entryPoint.transform.position);
            }
        }

        private void OnDrawGizmosSelected() {
            if (entryPoint == null) {
                return;
            }

            if (string.IsNullOrWhiteSpace(link.TargetDoorId)) {
                return;
            }

            Gizmos.color = new Color(0, 0, 1, 0.7f);
            var pos = entryPoint.transform.position;

            if (link.TargetScene.ScenePath != gameObject.scene.path) {
                var len = 1.5f;
                var upPos = pos + (Vector3.up * len);
                Gizmos.DrawLine(pos, upPos);

#if UNITY_EDITOR
                var sceneName = link.TargetScene.GetSceneName();
                if (!string.IsNullOrWhiteSpace(sceneName)) {
                    var text = sceneName + "\n" + link.TargetDoorId;

                    var style = DoorGizmoLabelStyle.Get();

                    var content = new GUIContent(text);
                    var size = style.CalcSize(content);
                    var padding = new Vector2(12f, 8f);

                    var guiPoint = HandleUtility.WorldToGUIPoint(upPos);

                    var rect = new Rect(
                        guiPoint.x - (size.x + padding.x) * 0.5f,
                        guiPoint.y - (size.y + padding.y) * 0.5f,
                        size.x + padding.x,
                        size.y + padding.y
                    );

                    Handles.BeginGUI();

                    var labelRect = new Rect(
                        rect.x + padding.x * 0.5f,
                        rect.y + padding.y * 0.5f,
                        rect.width - padding.x,
                        rect.height - padding.y
                    );

                    EditorGUI.LabelField(labelRect, text, style);
                    Handles.EndGUI();
                }
#endif
            }
        }

#if UNITY_EDITOR
        private const int DefaultGeneratedLength = 5;

        private void OnValidate() {
            if (Application.isPlaying) {
                return;
            }

            // If this component is on a prefab asset (Prefab Mode / project prefab),
            // keep DoorId empty so it does NOT propagate to instances.
            if (PrefabUtility.IsPartOfPrefabAsset(this)) {
                if (!string.IsNullOrEmpty(doorId)) {
                    doorId = string.Empty;
                    EditorUtility.SetDirty(this);
                }

                return;
            }

            // If this is a prefab instance and it still matches the prefab's stored id,
            // generate a unique one for this instance.
            var source = PrefabUtility.GetCorrespondingObjectFromSource(this) as Door;
            var isInheritedFromPrefab =
                source != null && string.Equals(doorId, source.doorId, StringComparison.Ordinal);

            if (string.IsNullOrWhiteSpace(doorId) || isInheritedFromPrefab) {
                doorId = $"Door_{DoorIdUtils.GenerateId(DefaultGeneratedLength)}";
                EditorUtility.SetDirty(this);
            }
        }

        public void EditorSetDoorId(string newId) {
            doorId = newId;
        }

        internal static class DoorGizmoLabelStyle {
            private static GUIStyle style;
            private static Texture2D bg;

            public static GUIStyle Get() {
                if (style != null) {
                    return style;
                }

                bg = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                bg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.35f));
                bg.Apply();

                style = new GUIStyle(EditorStyles.boldLabel) {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    richText = false,
                    wordWrap = false
                };

                style.normal.textColor = Color.white;
                style.normal.background = bg;
                style.padding = new RectOffset(8, 8, 5, 5);

                return style;
            }
        }
#endif
    }
}
