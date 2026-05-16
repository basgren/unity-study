using System;
using Game.Core.Services.Scene;
using Game.Core.Utils;
using Game.Features.Portals;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Features.Doors {
    /// <summary>
    /// A scene entrance that transitions the player to another entrance (in this or another scene)
    /// the moment the player enters its trigger collider. Unlike <see cref="Door"/>, entrances are
    /// not interactable — there is no button press, no open/close animation. Entrances only connect
    /// to other entrances (the editor tooling and validator enforce this kind constraint).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class Entrance : MonoBehaviour, IPortal {
        private const string PlayerTag = "Player";

        [SerializeField, HideInInspector]
        private string entranceId;

        [Tooltip("Destination scene and entrance the player is sent to when entering this trigger. " +
                 "Entrances can only target other entrances.")]
        [SerializeField]
        private EntranceLink link;

        [Tooltip("World position the player is teleported to when arriving at this entrance. " +
                 "Place it OUTSIDE the trigger collider so the player does not immediately re-trigger travel. " +
                 "If left empty, the entrance's own transform position is used.")]
        [SerializeField]
        private Transform entryPoint;

        [Tooltip("Invoked after the player has finished traveling to this entrance. " +
                 "Hook audio cues or other arrival effects here.")]
        [SerializeField]
        private UnityEvent onEntered;

        /// <summary>
        /// Entrance identifier. Must be unique within a scene.
        /// Not editable directly; use the editor "Change ID" action to rename safely.
        /// </summary>
        public string EntranceId => entranceId;

        /// <summary>
        /// Destination link for this entrance.
        /// </summary>
        public EntranceLink Link => link;

        string IPortal.Id => entranceId;
        SceneReference IPortal.TargetScene => link.TargetScene;
        string IPortal.TargetId => link.TargetEntranceId;

        public Vector3 GetEntryPosition() {
            if (entryPoint != null) {
                return entryPoint.position;
            }

            return transform.position;
        }

        /// <summary>
        /// Invoked after the player has arrived at this entrance. Wired by designers when audio or
        /// other side effects must play on arrival.
        /// </summary>
        public void NotifyEntered() {
            onEntered?.Invoke();
        }

        private void OnTriggerEnter2D(Collider2D other) {
            if (!other.CompareTag(PlayerTag)) {
                return;
            }

            // Suppress re-triggering while a travel is in progress (e.g. player teleports onto the
            // destination entrance collider before fade-in completes).
            if (PortalTravelService.IsTraveling) {
                return;
            }

            EntranceTravelService.Travel(this);
        }

        private static readonly Color GizmoSpawnColor = new Color(1f, 0.85f, 0f, 0.9f);
        private static readonly Color GizmoLinkColor = new Color(0f, 1f, 0.4f, 0.7f);
        private const float GizmoCrossSceneHintLength = 1.5f;

        private void OnDrawGizmos() {
            var center = GetColliderCenter();
            var spawnPos = GetEntryPosition();

            // Spawn marker (yellow): where the player materializes when arriving HERE.
            Gizmos.color = GizmoSpawnColor;
            Gizmos.DrawSphere(spawnPos, 0.12f);

            if (string.IsNullOrWhiteSpace(link.TargetEntranceId)) {
                return;
            }

            // Connection hint from the collider center: line to the target collider in the same scene,
            // or a short upward stub for cross-scene targets (full label shown when selected).
            Gizmos.color = GizmoLinkColor;

            if (link.TargetScene.ScenePath == gameObject.scene.path) {
                var targetEntrance = EntranceUtils.FindEntranceByIdInScene(gameObject.scene, link.TargetEntranceId);
                if (targetEntrance != null) {
                    Gizmos.DrawLine(center, targetEntrance.GetColliderCenter());
                }
            } else {
                Gizmos.DrawLine(center, center + (Vector3.up * GizmoCrossSceneHintLength));
            }
        }

        private void OnDrawGizmosSelected() {
            if (string.IsNullOrWhiteSpace(link.TargetEntranceId)) {
                return;
            }

#if UNITY_EDITOR
            var center = GetColliderCenter();
            var labelPos = center + (Vector3.up * GizmoCrossSceneHintLength);

            string text;
            if (link.TargetScene.ScenePath == gameObject.scene.path) {
                text = "→ " + link.TargetEntranceId;
            } else {
                var sceneName = link.TargetScene.GetSceneName();
                if (string.IsNullOrWhiteSpace(sceneName)) {
                    return;
                }

                text = "→ " + sceneName + "\n" + link.TargetEntranceId;
            }

            var style = EntranceGizmoLabelStyle.Get();
            var content = new GUIContent(text);
            var size = style.CalcSize(content);
            var padding = new Vector2(12f, 8f);

            var guiPoint = HandleUtility.WorldToGUIPoint(labelPos);

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
#endif
        }

        private Vector3 GetColliderCenter() {
            var col = GetComponent<Collider2D>();
            if (col != null) {
                return col.bounds.center;
            }

            return transform.position;
        }

#if UNITY_EDITOR
        private const int DefaultGeneratedLength = 5;

        private void OnValidate() {
            if (Application.isPlaying) {
                return;
            }

            // If this component is on a prefab asset (Prefab Mode / project prefab),
            // keep EntranceId empty so it does NOT propagate to instances.
            if (PrefabUtility.IsPartOfPrefabAsset(this)) {
                if (!string.IsNullOrEmpty(entranceId)) {
                    entranceId = string.Empty;
                    EditorUtility.SetDirty(this);
                }

                return;
            }

            // If this is a prefab instance and it still matches the prefab's stored id,
            // generate a unique one for this instance.
            var source = PrefabUtility.GetCorrespondingObjectFromSource(this) as Entrance;
            var isInheritedFromPrefab =
                source != null && string.Equals(entranceId, source.entranceId, StringComparison.Ordinal);

            if (string.IsNullOrWhiteSpace(entranceId) || isInheritedFromPrefab) {
                entranceId = $"Entrance_{IdUtils.GenerateId(DefaultGeneratedLength)}";
                EditorUtility.SetDirty(this);
            }
        }

        public void EditorSetEntranceId(string newId) {
            entranceId = newId;
        }

        internal static class EntranceGizmoLabelStyle {
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
