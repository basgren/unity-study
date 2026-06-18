using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Core.Components.Camera {
    /// <summary>
    /// Switches a <see cref="CinemachineConfiner2D"/>'s bounding shape between any number of named
    /// states. Author a list of <c>(id, shape)</c> entries, then call <see cref="Select"/> with an id
    /// to swap the confiner to that shape (after the optional <see cref="delay"/>), or
    /// <see cref="SelectImmediate"/> to apply at once (used by state restore). The confiner's own
    /// <c>m_Damping</c> eases the camera to the new bounds; this component only changes the shape and
    /// invalidates the cache.
    ///
    /// Generalises the deprecated binary <see cref="SwapCameraConfinerShape"/> (default → expanded) to
    /// an N-state progression — e.g. a boss room's small → big (floor broken) → final (defeated)
    /// confiners, driven by an encounter director reacting to milestones.
    ///
    /// Persistence: <see cref="CurrentStateId"/> plus <see cref="SelectImmediate"/> let a state saver
    /// capture the active state and re-apply it without replaying the original event.
    /// </summary>
    public class CameraConfinerSelector : MonoBehaviour {
        /// <summary>One authored confiner state: an id selected from code, and the shape to apply.</summary>
        [Serializable]
        public struct ConfinerState {
            [Tooltip("Stable id used to select this state from code (e.g. \"Big\", \"Final\").")]
            public string id;

            [Tooltip("Bounding shape the confiner switches to for this state.")]
            public Collider2D shape;
        }

        [SerializeField]
        [Tooltip("Confiner whose bounding shape is switched.")]
        private CinemachineConfiner2D confiner;

        [SerializeField]
        [Tooltip("Authored states. Each id should be unique; Select(id) / SelectImmediate(id) picks one.")]
        private ConfinerState[] states;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds to wait before an animated Select() swap takes effect. SelectImmediate ignores it.")]
        private float delay;

        private Coroutine pendingRoutine;
        private string currentStateId;

        /// <summary>Id of the state currently applied (empty until the first selection). For persistence.</summary>
        public string CurrentStateId => currentStateId;

        /// <summary>
        /// Switches to the state with the given id after <see cref="delay"/> seconds (or immediately when
        /// the delay is zero). Re-entrant calls cancel any pending swap so the latest request wins.
        /// Unknown ids are ignored with a warning.
        /// </summary>
        public void Select(string id) {
            if (!StateExists(id)) {
                Debug.LogWarning($"CameraConfinerSelector: no state '{id}'.", this);
                return;
            }

            CancelPending();

            if (delay > 0f) {
                pendingRoutine = StartCoroutine(SelectAfterDelay(id));
            } else {
                Apply(id);
            }
        }

        /// <summary>Switches to the given state immediately, bypassing the delay (e.g. state restore).</summary>
        public void SelectImmediate(string id) {
            if (!StateExists(id)) {
                Debug.LogWarning($"CameraConfinerSelector: no state '{id}'.", this);
                return;
            }

            CancelPending();
            Apply(id);
        }

        private IEnumerator SelectAfterDelay(string id) {
            yield return new WaitForSeconds(delay);
            Apply(id);
            pendingRoutine = null;
        }

        private void Apply(string id) {
            Collider2D shape = ResolveShape(id);
            if (confiner == null || shape == null) {
                return;
            }

            confiner.BoundingShape2D = shape;
            // The confiner caches the computed bounds, so it ignores the new shape until invalidated.
            confiner.InvalidateBoundingShapeCache();
            currentStateId = id;
        }

        private void CancelPending() {
            if (pendingRoutine != null) {
                StopCoroutine(pendingRoutine);
                pendingRoutine = null;
            }
        }

        private bool StateExists(string id) {
            if (states == null || string.IsNullOrEmpty(id)) {
                return false;
            }

            for (int i = 0; i < states.Length; i++) {
                if (states[i].id == id) {
                    return true;
                }
            }

            return false;
        }

        private Collider2D ResolveShape(string id) {
            if (states == null) {
                return null;
            }

            for (int i = 0; i < states.Length; i++) {
                if (states[i].id == id) {
                    return states[i].shape;
                }
            }

            return null;
        }
    }
}
