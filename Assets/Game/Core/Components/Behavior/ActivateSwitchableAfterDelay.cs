using System.Collections;
using Game.Core.Components.Interaction;
using UnityEngine;

namespace Core.Components.Behavior {
    /// <summary>
    /// Sets a target <see cref="SwitchableBase"/> to a chosen state after a delay when
    /// <see cref="Trigger"/> is called. Wire <see cref="Trigger"/> to a UnityEvent — e.g. a
    /// boss's <c>Damageable.onDeath</c> — to open a gate a few seconds after the event fires.
    /// </summary>
    public class ActivateSwitchableAfterDelay : MonoBehaviour {
        [Tooltip("Switchable to drive. Defaults to a SwitchableBase on this GameObject if left empty.")]
        [SerializeField]
        private SwitchableBase target;

        [Tooltip("Seconds to wait after Trigger() before changing the target's state.")]
        [SerializeField]
        [Min(0f)]
        private float delay = 3f;

        [Tooltip("Value applied to the target's IsActive once the delay elapses.")]
        [SerializeField]
        private bool targetActive = true;

        private Coroutine pendingRoutine;

        private void Awake() {
            if (target == null) {
                target = GetComponent<SwitchableBase>();
            }
        }

        /// <summary>
        /// Starts the delayed activation. Ignored if a previous trigger is still pending,
        /// so re-entrant calls (or a doubled event) cannot stack coroutines.
        /// </summary>
        public void Trigger() {
            if (target == null || pendingRoutine != null) {
                return;
            }

            pendingRoutine = StartCoroutine(ActivateAfterDelay());
        }

        private IEnumerator ActivateAfterDelay() {
            if (delay > 0f) {
                yield return new WaitForSeconds(delay);
            }

            target.IsActive = targetActive;
            pendingRoutine = null;
        }
    }
}
