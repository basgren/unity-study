using UnityEngine;

namespace Game.Features.Bosses.VengefulSpirit.Teleport {
    /// <summary>
    /// Marker component placed on an empty GameObject at a teleport destination.
    /// The boss reads its transform position when relocating during a teleport.
    /// Drop several anchors into the boss room scene and wire them into the
    /// boss's <c>teleportAnchors</c> array.
    /// </summary>
    public class TeleportAnchor: MonoBehaviour {
#if UNITY_EDITOR
        private void DrawAnchorMarker() {
            Gizmos.color = new Color(1f, 0.5f, 1f, 0.9f);
            Vector3 p = transform.position;
            Gizmos.DrawWireSphere(p, 0.25f);
            Gizmos.DrawLine(p + Vector3.left * 0.4f, p + Vector3.right * 0.4f);
            Gizmos.DrawLine(p + Vector3.up * 0.4f, p + Vector3.down * 0.4f);

            UnityEditor.Handles.color = new Color(1f, 0.5f, 1f, 0.9f);
            UnityEditor.Handles.Label(p + new Vector3(0.4f, 0.3f, 0f), $"Teleport Anchor: {name}");
        }

        private void OnDrawGizmos() {
            DrawAnchorMarker();
        }
#endif
    }
}
