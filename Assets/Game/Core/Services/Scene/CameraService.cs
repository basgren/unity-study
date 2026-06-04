using System.Collections;
using Cinemachine;
using UnityEngine;

namespace Game.Core.Services.Scene {
    /// <summary>
    /// Provides camera utility operations used by gameplay and scene transitions.
    /// </summary>
    public class CameraService : MonoBehaviour {
        private Transform pendingTarget;
        private Vector3 pendingPositionDelta;

        private CinemachineBasicMultiChannelPerlin cachedNoise;
        private Coroutine shakeCoroutine;
        private float currentShakeAmplitude;
        private float currentShakeFrequency;

        private void LateUpdate() {
            TryApplyPendingTeleports();
        }

        /// <summary>
        /// Notifies the camera system that <paramref name="target"/> was teleported by <paramref name="positionDelta"/>.
        /// If no matching Cinemachine camera is ready yet, the request is queued and retried.
        /// </summary>
        public void NotifyTargetTeleported(Transform target, Vector3 positionDelta) {
            if (target == null || positionDelta.sqrMagnitude <= Mathf.Epsilon) {
                return;
            }

            if (TryNotifyTargetTeleported(target, positionDelta)) {
                return;
            }

            QueuePendingTeleport(target, positionDelta);
        }

        /// <summary>
        /// Tries to apply all queued teleport notifications.
        /// Returns true when no pending notification remains.
        /// </summary>
        public bool TryApplyPendingTeleports() {
            if (pendingTarget == null) {
                return true;
            }

            if (pendingPositionDelta.sqrMagnitude <= Mathf.Epsilon) {
                ClearPendingTeleport();
                return true;
            }

            if (!TryNotifyTargetTeleported(pendingTarget, pendingPositionDelta)) {
                return false;
            }

            ClearPendingTeleport();
            return true;
        }

        private void QueuePendingTeleport(Transform target, Vector3 positionDelta) {
            if (pendingTarget == target) {
                pendingPositionDelta += positionDelta;
            } else {
                pendingTarget = target;
                pendingPositionDelta = positionDelta;
            }
        }

        private void ClearPendingTeleport() {
            pendingTarget = null;
            pendingPositionDelta = Vector3.zero;
        }

        private bool TryNotifyTargetTeleported(Transform target, Vector3 positionDelta) {
            var cinemachineCore = CinemachineCore.Instance;
            if (cinemachineCore == null) {
                return false;
            }

            bool applied = false;
            int cameraCount = cinemachineCore.VirtualCameraCount;
            for (int i = 0; i < cameraCount; i++) {
                var cam = cinemachineCore.GetVirtualCamera(i);
                if (cam == null) {
                    continue;
                }

                if (cam.Follow != target && cam.LookAt != target) {
                    continue;
                }

                // Treat teleport as a cut and drop old damping state.
                cam.PreviousStateIsValid = false;
                cam.OnTargetObjectWarped(target, positionDelta);
                applied = true;
            }

            return applied;
        }

        /// <summary>
        /// Shakes the camera with the given settings. A new shake only takes over when its
        /// amplitude is at least the running shake's current (possibly decayed) amplitude,
        /// so a weak shake never cuts off a strong one mid-impact.
        /// </summary>
        public void Shake(CameraShakeSettings settings) {
            if (settings.duration <= 0f || settings.amplitude <= 0f) {
                return;
            }

            // Strongest wins: ignore shakes weaker than what is currently playing.
            if (shakeCoroutine != null && settings.amplitude < currentShakeAmplitude) {
                return;
            }

            var noise = ResolveNoise();
            if (noise == null) {
                return;
            }

            StopShake();
            shakeCoroutine = StartCoroutine(RunShake(noise, settings));
        }

        /// <summary>
        /// Shakes the camera holding full amplitude, then fading out over the last
        /// <paramref name="decayDuration"/> seconds (clamped to <paramref name="duration"/>).
        /// </summary>
        public void Shake(float amplitude, float duration, float decayDuration) {
            Shake(new CameraShakeSettings {
                amplitude = amplitude,
                frequency = CameraShakeSettings.Default.frequency,
                duration = duration,
                decayDuration = decayDuration
            });
        }

        /// <summary>
        /// Shakes the camera with amplitude decaying linearly over the whole duration.
        /// </summary>
        public void Shake(float amplitude, float duration) {
            Shake(amplitude, duration, duration);
        }

        /// <summary>
        /// Starts an open-ended shake that holds full amplitude until <see cref="StopShake"/> or
        /// <see cref="StopShakeWithDecay"/> is called (or a stronger timed shake takes over).
        /// </summary>
        public void StartContinuousShake(float amplitude, float frequency) {
            Shake(new CameraShakeSettings {
                amplitude = amplitude,
                frequency = frequency,
                duration = float.PositiveInfinity,
                decayDuration = 0f
            });
        }

        /// <summary>
        /// Ends the running shake (if any) by fading its current amplitude to zero over
        /// <paramref name="decayDuration"/> seconds instead of cutting it off.
        /// </summary>
        public void StopShakeWithDecay(float decayDuration) {
            if (shakeCoroutine == null) {
                return;
            }

            if (decayDuration <= 0f) {
                StopShake();
                return;
            }

            var noise = ResolveNoise();
            if (noise == null) {
                StopShake();
                return;
            }

            // Restart the envelope from the current (possibly decayed) amplitude as a pure decay tail.
            var settings = new CameraShakeSettings {
                amplitude = currentShakeAmplitude,
                frequency = currentShakeFrequency,
                duration = decayDuration,
                decayDuration = decayDuration
            };

            StopCoroutine(shakeCoroutine);
            shakeCoroutine = StartCoroutine(RunShake(noise, settings));
        }

        /// <summary>Stops the running shake (if any) and resets the camera noise.</summary>
        public void StopShake() {
            if (shakeCoroutine == null) {
                return;
            }

            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
            currentShakeAmplitude = 0f;
            ResetNoise();
        }

        private IEnumerator RunShake(CinemachineBasicMultiChannelPerlin noise, CameraShakeSettings settings) {
            float decay = Mathf.Clamp(settings.decayDuration, 0f, settings.duration);
            float stopTime = Time.time + settings.duration;
            float decayStartTime = stopTime - decay;

            noise.m_FrequencyGain = settings.frequency;
            currentShakeFrequency = settings.frequency;

            while (Time.time < stopTime) {
                if (noise == null) {
                    // Scene change destroyed the camera mid-shake; nothing left to reset.
                    shakeCoroutine = null;
                    currentShakeAmplitude = 0f;
                    yield break;
                }

                float amp = settings.amplitude;
                if (decay > 0f && Time.time > decayStartTime) {
                    float decayProgress = (Time.time - decayStartTime) / decay;
                    amp = Mathf.Lerp(settings.amplitude, 0f, decayProgress);
                }

                currentShakeAmplitude = amp;
                noise.m_AmplitudeGain = amp;
                yield return null;
            }

            shakeCoroutine = null;
            currentShakeAmplitude = 0f;
            ResetNoise();
        }

        private CinemachineBasicMultiChannelPerlin ResolveNoise() {
            if (cachedNoise != null) {
                return cachedNoise;
            }

            var cinemachineCore = CinemachineCore.Instance;
            if (cinemachineCore == null) {
                return null;
            }

            // The noise-equipped vcam lives in the per-scene GlobalRoot, so the cache goes
            // stale on scene change and we re-resolve from the registered cameras.
            int cameraCount = cinemachineCore.VirtualCameraCount;
            for (int i = 0; i < cameraCount; i++) {
                var cam = cinemachineCore.GetVirtualCamera(i) as CinemachineVirtualCamera;
                if (cam == null) {
                    continue;
                }

                var noise = cam.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
                if (noise != null) {
                    cachedNoise = noise;
                    return noise;
                }
            }

            return null;
        }

        private void ResetNoise() {
            if (cachedNoise == null) {
                return;
            }

            cachedNoise.m_AmplitudeGain = 0f;
            cachedNoise.m_FrequencyGain = 0f;
        }
    }
}
