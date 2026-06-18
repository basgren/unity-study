using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Features.Effects.Camera {
    public class CameraShakeEffect : MonoBehaviour {
        [SerializeField]
        private float duration = 0.3f;

        [SerializeField]
        private float amplitude = 1.5f;

        [SerializeField]
        private float frequency = 3f;

        [SerializeField]
        private bool shakeOnAwake;

        private CinemachineBasicMultiChannelPerlin noise;
        private Coroutine coroutine;
        private float stopTime;

        private void Awake() {
            var vCamera = FindAnyObjectByType<CinemachineCamera>();
            // CM3: noise is a plain component on the vcam GameObject (was GetCinemachineComponent in CM2).
            noise = vCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();

            if (shakeOnAwake) {
                Shake();
            }
        }

        public void Shake() {
            StopShaking();
            coroutine = StartCoroutine(StartShaking());
        }

        private IEnumerator StartShaking() {
            stopTime = Time.time + duration;
            noise.FrequencyGain = frequency;

            float remainingProgress;

            do {
                remainingProgress = (stopTime - Time.time) / duration;
                var amp = Mathf.Lerp(0f, amplitude, remainingProgress);
                noise.AmplitudeGain = amp;
                yield return null;
            } while (remainingProgress > 0);

            StopShaking();
        }

        private void StopShaking() {
            if (coroutine == null) {
                return;
            }

            noise.FrequencyGain = 0f;
            noise.AmplitudeGain = 0f;

            StopCoroutine(coroutine);
            coroutine = null;
        }
    }
}
