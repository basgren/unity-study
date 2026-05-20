using System.Collections;
using Game.Core.Components.Interaction;
using UnityEngine;
using UnityEngine.Events;

namespace Core.Components.Behavior {
    /// <summary>
    /// Emits <see cref="onTrigger"/> events while active.
    /// </summary>
    /// <remarks>
    /// Behavior is organized in repeating series:
    /// 1. Wait <see cref="startDelay"/> after activation.
    /// 2. Emit the first event in a series.
    /// 3. Emit the remaining <see cref="eventsInSeries"/> - 1 events with <see cref="eventDelay"/> between them.
    /// 4. Start the next series so the time between first events of consecutive series is <see cref="seriesPeriod"/>.
    ///
    /// Setting <see cref="IsActive"/> to <c>false</c> stops all pending events immediately.
    /// Setting it back to <c>true</c> starts the sequence from the beginning (including start delay).
    /// </remarks>
    public class AutoTrigger : SwitchableBase {
        [SerializeField]
        private bool isActive;

        [SerializeField]
        private UnityEvent onTrigger = new();

        [SerializeField]
        [Min(0f)]
        private float seriesPeriod = 1f;

        [SerializeField]
        [Min(1)]
        private int eventsInSeries = 1;

        [SerializeField]
        [Min(0.1f)]
        private float eventDelay = 0.5f;

        [SerializeField]
        [Min(0f)]
        private float startDelay;

        private Coroutine triggerRoutine;
        private bool hasStarted;

        private OnSwitchChangeEvent onChange = new();
        protected override OnSwitchChangeEvent ChangeEvent => onChange;

        public override bool IsActive {
            get => isActive;
            set {
                if (isActive == value) {
                    return;
                }

                SetIsActive(ref isActive, value);

                if (isActive) {
                    // We'll delay StartTriggering, as when startDelay = 0, this event may
                    // be triggered earlier than subscriber has `Awake` run and fields initialized.
                    if (hasStarted && isActiveAndEnabled) {
                        StartTriggeringFromBeginning();
                    }
                } else {
                    StopTriggering();
                }
            }
        }

        private void OnEnable() {
            if (hasStarted && isActive) {
                StartTriggeringFromBeginning();
            }
        }

        private void Start() {
            hasStarted = true;

            if (isActive && isActiveAndEnabled) {
                StartTriggeringFromBeginning();
            }
        }

        private void OnDisable() {
            StopTriggering();
        }

        private void StartTriggeringFromBeginning() {
            StopTriggering();

            if (!isActive || !isActiveAndEnabled) {
                return;
            }

            triggerRoutine = StartCoroutine(TriggerLoop());
        }

        private void StopTriggering() {
            if (triggerRoutine == null) {
                return;
            }

            StopCoroutine(triggerRoutine);
            triggerRoutine = null;
        }

        private IEnumerator TriggerLoop() {
            if (startDelay > 0f) {
                yield return new WaitForSeconds(startDelay);
            }

            var safeEventsInSeries = Mathf.Max(1, eventsInSeries);
            var safeEventDelay = Mathf.Max(0f, eventDelay);
            var safeSeriesPeriod = Mathf.Max(0f, seriesPeriod);

            while (isActive) {
                var seriesStart = Time.time;
                onTrigger?.Invoke();

                for (var i = 1; i < safeEventsInSeries; i++) {
                    if (!isActive) {
                        yield break;
                    }

                    if (safeEventDelay > 0f) {
                        yield return new WaitForSeconds(safeEventDelay);
                    }

                    if (!isActive) {
                        yield break;
                    }

                    onTrigger?.Invoke();
                }

                var elapsed = Time.time - seriesStart;
                var waitForNextSeries = safeSeriesPeriod - elapsed;

                if (waitForNextSeries > 0f) {
                    yield return new WaitForSeconds(waitForNextSeries);
                } else {
                    // Avoid a tight loop when period is shorter than series duration.
                    yield return null;
                }
            }

            triggerRoutine = null;
        }

#if UNITY_EDITOR
        private void OnValidate() {
            seriesPeriod = Mathf.Max(0f, seriesPeriod);
            eventsInSeries = Mathf.Max(1, eventsInSeries);
            eventDelay = Mathf.Max(0f, eventDelay);
            startDelay = Mathf.Max(0f, startDelay);
        }
#endif
    }
}
