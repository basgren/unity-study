using Game.Core.Components.Base2D;
using UnityEngine;

namespace Game.Features.Bosses.VengefulSpirit.Teleport {
    /// <summary>
    /// Marker component placed on an empty GameObject at a teleport destination.
    /// The boss reads its transform position when relocating during a teleport.
    /// Drop several anchors into the boss room scene and wire them into the relevant
    /// pattern components (or the AI's reactive-escape list).
    /// </summary>
    public class TeleportAnchor: MonoBehaviour {
        [Tooltip("Facing direction the boss should snap to when reappearing at this anchor.")]
        [SerializeField]
        private FacingDir facingDir = FacingDir.Right;

        public FacingDir FacingDir => facingDir;


#if UNITY_EDITOR
        private void DrawAnchorMarker() {
            Color markerColor = new Color(1f, 0.5f, 1f, 0.9f);

            Gizmos.color = markerColor;
            Vector3 p = transform.position;
            Gizmos.DrawWireSphere(p, 0.25f);
            Gizmos.DrawLine(p + Vector3.left * 0.4f, p + Vector3.right * 0.4f);
            Gizmos.DrawLine(p + Vector3.up * 0.4f, p + Vector3.down * 0.4f);

            DrawFacingArrow(p);

            UnityEditor.Handles.color = markerColor;
            UnityEditor.Handles.Label(p + new Vector3(0.4f, 0.3f, 0f), $"Teleport Anchor: {name}");
        }

        // Draws a short arrow in the facing direction so designers can see at a glance
        // which way the boss will face when reappearing here.
        private void DrawFacingArrow(Vector3 origin) {
            int sign = (int)facingDir;
            const float shaftLength = 0.7f;
            const float headLength = 0.2f;
            const float headSpread = 0.15f;
            Vector3 tip = origin + new Vector3(sign * shaftLength, 0f, 0f);

            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Gizmos.DrawLine(origin, tip);
            Gizmos.DrawLine(tip, tip + new Vector3(-sign * headLength,  headSpread, 0f));
            Gizmos.DrawLine(tip, tip + new Vector3(-sign * headLength, -headSpread, 0f));
        }

        private void OnDrawGizmos() {
            DrawAnchorMarker();
        }
#endif
    }
}
