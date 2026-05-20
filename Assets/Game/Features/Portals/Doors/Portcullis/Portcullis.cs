using System.Collections;
using Game.Core.Bootstrap;
using Game.Core.Components.Interaction;
using UnityEngine;

namespace Game.Features.Portals.Doors.Portcullis
{
    /// <summary>
    /// A gate door driven by a <see cref="Switchable"/>. When the switchable becomes active the
    /// front grate slides up (open); when it becomes inactive the grate slides back down (closed).
    /// </summary>
    /// <remarks>
    /// The grate travels exactly the height of the parent Portcullis sprite so it tucks fully
    /// behind the frame when open. The blocking collider is disabled only once the gate is fully
    /// open, and re-enabled the instant it starts closing.
    /// </remarks>
    [RequireComponent(typeof(Switchable))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class Portcullis : MonoBehaviour {
        [SerializeField]
        private GameObject portcullisFront;

        [Tooltip("Seconds for the grate to travel its full open/close distance.")]
        [SerializeField]
        private float moveDuration = 0.5f;

        [Tooltip("Distance the grate travels up to fully open, in local units. " +
                 "Leave at 0 to auto-use the height of the back (frame) sprite.")]
        [SerializeField]
        private float openDistance;

        private Switchable switchable;
        private SpriteRenderer frameRenderer;
        private Collider2D blockingCollider;

        private Vector3 closedLocalPosition;

        private Coroutine moveCoroutine;

        // Default travel is the back (frame) sprite height so the grate hides fully behind it;
        // an explicit openDistance overrides it. Read live so Inspector tuning applies on the next toggle.
        private float OpenOffset => openDistance > 0f ? openDistance : frameRenderer.sprite.bounds.size.y;

        private Vector3 OpenLocalPosition => closedLocalPosition + Vector3.up * OpenOffset;

        private void Awake() {
            switchable = GetComponent<Switchable>();
            frameRenderer = GetComponent<SpriteRenderer>();
            blockingCollider = GetComponent<Collider2D>();

            closedLocalPosition = portcullisFront.transform.localPosition;

            ApplyStateInstant(switchable.IsActive);
        }

        private void OnEnable() {
            switchable.Changed += OnSwitchableChanged;
        }

        private void OnDisable() {
            switchable.Changed -= OnSwitchableChanged;
        }

        private void OnSwitchableChanged(bool isOpen) {
            // During scene state restore, snap to the target state without animating.
            if (G.SceneState != null && G.SceneState.IsRestoring) {
                ApplyStateInstant(isOpen);
                return;
            }

            if (moveCoroutine != null) {
                StopCoroutine(moveCoroutine);
            }

            moveCoroutine = StartCoroutine(Move(isOpen));
        }

        private IEnumerator Move(bool isOpen) {
            // Closing must block immediately; opening keeps blocking until fully open.
            if (!isOpen) {
                blockingCollider.enabled = true;
            }

            Vector3 from = portcullisFront.transform.localPosition;
            Vector3 to = isOpen ? OpenLocalPosition : closedLocalPosition;

            // Scale the duration by the remaining distance so an interrupted move keeps a constant speed.
            float fullOffset = OpenOffset;
            float duration = fullOffset > 0f
                ? moveDuration * (Vector3.Distance(from, to) / fullOffset)
                : 0f;

            float elapsed = 0f;
            while (elapsed < duration) {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                portcullisFront.transform.localPosition = Vector3.Lerp(from, to, t);
                yield return null;
            }

            portcullisFront.transform.localPosition = to;

            // Fully open: stop blocking. Fully closed: stays blocking (enabled above).
            if (isOpen) {
                blockingCollider.enabled = false;
            }

            moveCoroutine = null;
        }

        private void ApplyStateInstant(bool isOpen) {
            portcullisFront.transform.localPosition = isOpen ? OpenLocalPosition : closedLocalPosition;
            blockingCollider.enabled = !isOpen;
        }
    }
}
