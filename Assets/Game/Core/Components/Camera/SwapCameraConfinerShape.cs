using System;
using System.Collections;
using Cinemachine;
using UnityEngine;

namespace Game.Core.Components.Camera {
    /// <summary>
    /// DEPRECATED — use <see cref="CameraConfinerSelector"/> for new work. This binary swap only
    /// supports one default → one expanded shape; the selector handles any number of named states
    /// (e.g. a boss room's small → big → final confiners). Kept fully functional for the existing
    /// scenes that already wire it (SH_LastWarmFloor, WS_CaptainsLastLaugh) so nothing breaks.
    ///
    /// Swaps a <see cref="CinemachineConfiner2D"/>'s bounding shape to a larger collider after a
    /// delay when <see cref="Trigger"/> is called, then invalidates the confiner cache so the new
    /// bounds take effect. Wire <see cref="Trigger"/> to a UnityEvent — e.g. a boss's
    /// <c>Damageable.onDeath</c> — to widen the camera area once an encounter ends.
    /// </summary>
    /// <remarks>
    /// The confiner's own <c>m_Damping</c> drives how smoothly the camera eases out to the new
    /// bounds; this component only changes the shape and refreshes the cache.
    /// <see cref="IsExpanded"/> and <see cref="ApplyExpandedImmediate"/> let a state saver persist
    /// and re-apply the expansion across scene transitions.
    /// </remarks>
    // Hidden from the Add Component menu to discourage new uses; existing scene instances are
    // resolved by GUID and keep working.
    [AddComponentMenu("")]
    [Obsolete("Use CameraConfinerSelector for multi-state confiner switching. Kept for existing scenes.")]
    public class SwapCameraConfinerShape : MonoBehaviour {
        [Tooltip("Confiner whose bounding shape will be replaced.")]
        [SerializeField]
        private CinemachineConfiner2D confiner;

        [Tooltip("Collider the confiner switches to. Should fully contain the original bounds.")]
        [SerializeField]
        private Collider2D expandedShape;

        [Tooltip("Seconds to wait after Trigger() before swapping the shape.")]
        [SerializeField]
        [Min(0f)]
        private float delay;

        private Coroutine pendingRoutine;
        private bool isExpanded;

        /// <summary>True once the confiner has been switched to the expanded shape.</summary>
        public bool IsExpanded => isExpanded;

        /// <summary>
        /// Starts the delayed shape swap. Ignored if a previous trigger is still pending,
        /// so re-entrant calls (or a doubled event) cannot stack coroutines.
        /// </summary>
        public void Trigger() {
            if (confiner == null || expandedShape == null || pendingRoutine != null) {
                return;
            }

            pendingRoutine = StartCoroutine(SwapAfterDelay());
        }

        /// <summary>
        /// Applies the expanded shape immediately, bypassing the delay. Used by state restore so a
        /// returning player sees the widened bounds at once, without re-triggering the source event.
        /// </summary>
        public void ApplyExpandedImmediate() {
            ApplyExpanded();
        }

        private IEnumerator SwapAfterDelay() {
            if (delay > 0f) {
                yield return new WaitForSeconds(delay);
            }

            ApplyExpanded();
            pendingRoutine = null;
        }

        private void ApplyExpanded() {
            if (confiner == null || expandedShape == null) {
                return;
            }

            confiner.m_BoundingShape2D = expandedShape;
            // The confiner caches the computed bounds, so it ignores the new shape until invalidated.
            confiner.InvalidateCache();
            isExpanded = true;
        }
    }
}
