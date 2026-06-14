using System;
using System.Globalization;
using Game.Core.Services.Scene;
using Game.Core.Utils;
using Game.Features.Portals.Common;
using UnityEngine;
using UnityEngine.Events;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Features.Portals.Portal {
    /// <summary>
    /// A trigger portal that teleports the player to another portal the instant the player enters its
    /// collider. The destination may live in the same scene or another scene. Travel is one-way: a
    /// Portal with a link is a source; a Portal without a link is an inert destination / spawn point.
    /// Unlike Entrance there is no facing requirement and no walk-in/walk-out cinematic — the shared
    /// <see cref="PortalTravelService"/> default path (fade-out, optional scene load, teleport,
    /// fade-in) is used as-is.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class Portal : MonoBehaviour, IPortal {
        private const string PlayerTag = "Player";

        [SerializeField, HideInInspector]
        private string portalId;

        [Tooltip("Destination scene and portal the player is sent to. Leave empty to make this Portal " +
                 "a pure destination (inert when stepped on).")]
        [SerializeField]
        private PortalLink link;

        [Tooltip("World position the player is teleported to when arriving at this portal. " +
                 "Place it OUTSIDE the trigger collider so the player does not immediately re-trigger " +
                 "travel. If empty, this portal's own transform position is used.")]
        [SerializeField]
        private Transform entryPoint;

        [Tooltip("Invoked after the player has finished traveling to this portal. " +
                 "Hook audio cues or other arrival effects here.")]
        [SerializeField]
        private UnityEvent onEntered;

        /// <summary>Portal identifier. Numeric string ("1", "2", ...) auto-assigned per scene.</summary>
        public string PortalId => portalId;

        /// <summary>Destination link for this portal. Points to another Portal (filtered by kind in the drawer).</summary>
        public PortalLink Link => link;

        string IPortal.Id => portalId;
        SceneReference IPortal.TargetScene => link.TargetScene;
        string IPortal.TargetId => link.TargetId;

        public Vector3 GetEntryPosition() {
            if (entryPoint != null) {
                return entryPoint.position;
            }

            return transform.position;
        }

        /// <summary>
        /// Invoked after the player has arrived at this portal. Wired by designers when audio or
        /// other side effects must play on arrival.
        /// </summary>
        public void NotifyEntered() {
            onEntered?.Invoke();
        }

        private void OnTriggerEnter2D(Collider2D other) {
            if (!other.CompareTag(PlayerTag)) {
                return;
            }

            // A Portal with no link is a pure destination / spawn point. Without this guard, stepping
            // on it would fire a pointless fade-out/fade-in with no teleport.
            if (string.IsNullOrEmpty(link.TargetId)) {
                return;
            }

            // Suppress re-triggering while a travel is already in progress (e.g. the player overlaps
            // the destination collider right after arriving).
            if (PortalTravelService.IsTraveling) {
                return;
            }

            PortalTravelService.Travel(this, PortalUtils.FindPortalByIdInScene<Portal>);
        }

        private static readonly Color GizmoSpawnColor = new Color(1f, 0.85f, 0f, 0.9f);
        private static readonly Color GizmoLinkColor = new Color(0f, 1f, 0.4f, 0.7f);
        private const float GizmoCrossSceneHintLength = 1.5f;

        private void OnDrawGizmos() {
            var spawnPos = GetEntryPosition();

            // Entry marker (yellow): where the player ends up when arriving at this portal.
            Gizmos.color = GizmoSpawnColor;
            Gizmos.DrawSphere(spawnPos, 0.12f);

            if (string.IsNullOrEmpty(link.TargetId)) {
                return;
            }

            // Connection hint: line to the target collider in the same scene, or a short upward stub
            // for cross-scene targets.
            Gizmos.color = GizmoLinkColor;
            var center = GetColliderCenter();

            if (link.TargetScene.ScenePath == gameObject.scene.path) {
                var target = PortalUtils.FindPortalByIdInScene<Portal>(gameObject.scene, link.TargetId);
                if (target != null) {
                    Gizmos.DrawLine(center, target.GetColliderCenter());
                }
            } else {
                Gizmos.DrawLine(center, center + (Vector3.up * GizmoCrossSceneHintLength));
            }
        }

        private Vector3 GetColliderCenter() {
            var col = GetComponent<Collider2D>();
            if (col != null) {
                return col.bounds.center;
            }

            return transform.position;
        }

#if UNITY_EDITOR
        private void OnValidate() {
            if (Application.isPlaying) {
                return;
            }

            // Prefab asset itself never carries a per-scene id; clear it so it does not propagate to instances.
            if (PrefabUtility.IsPartOfPrefabAsset(this)) {
                if (!string.IsNullOrEmpty(portalId)) {
                    portalId = string.Empty;
                    EditorUtility.SetDirty(this);
                }

                return;
            }

            // Prefab instances inherit the source's (empty) id; reassign a fresh per-scene id.
            var source = PrefabUtility.GetCorrespondingObjectFromSource(this) as Portal;
            var isInheritedFromPrefab = source != null && string.Equals(portalId, source.portalId, StringComparison.Ordinal);

            if (string.IsNullOrEmpty(portalId) || isInheritedFromPrefab) {
                portalId = NextFreeIdInScene().ToString(CultureInfo.InvariantCulture);
                EditorUtility.SetDirty(this);
            }
        }

        /// <summary>
        /// Returns max numeric id of all Portals in this scene + 1, ignoring this instance and non-numeric ids.
        /// </summary>
        private int NextFreeIdInScene() {
            var portals = PortalUtils.GetPortalsInScene<Portal>(gameObject.scene);
            int max = 0;
            for (var i = 0; i < portals.Count; i++) {
                var other = portals[i];
                if (other == null || other == this) {
                    continue;
                }

                if (IdUtils.TryParsePortalId(other.portalId, out var otherId) && otherId > max) {
                    max = otherId;
                }
            }

            return max + 1;
        }

        public void EditorSetPortalId(string newId) {
            portalId = newId;
        }
#endif
    }
}
