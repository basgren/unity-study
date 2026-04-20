using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Core.Components.Effects {
    /// <summary>
    /// Simulates the flickering light of a flame source (candle, torch, bonfire).
    /// Applies a coherent Perlin-noise based position shake and intensity flicker
    /// to one or more Light2D objects, optionally punctuated by random intensity bursts.
    /// Each light keeps its own baseline intensity and local position; the flicker
    /// scales them proportionally so layered lights (e.g. wide ground fill + narrow
    /// wall light) flicker together while preserving their authored balance.
    /// </summary>
    public class FireLightFlicker : MonoBehaviour {
        [SerializeField]
        private Light2D[] lights;

        [Header("Position Shake")]
        [SerializeField]
        private Vector2 shakeAmplitude = new Vector2(0.03f, 0.03f);

        [SerializeField]
        private float shakeFrequency = 4.0f;

        [Header("Intensity Flicker")]
        [Range(0.0f, 1.0f)]
        [SerializeField]
        private float intensityVariation = 0.15f;

        [SerializeField]
        private float flickerFrequency = 6.0f;

        [Header("Bursts")]
        [SerializeField]
        private bool enableBursts;

        [SerializeField]
        private float burstChancePerSecond = 0.5f;

        [SerializeField]
        private float burstIntensityBonus = 0.5f;

        [SerializeField]
        private float burstDuration = 0.1f;

        private Vector3[] baselinePositions;
        private float[] baselineIntensities;

        // Random offsets sample independent slices of the Perlin field, so multiple
        // FireLightFlicker instances in the same scene don't visibly synchronize.
        private float noiseOffsetX;
        private float noiseOffsetY;
        private float noiseOffsetIntensity;

        private float burstRemaining;

        void Awake() {
            CacheBaselines();

            noiseOffsetX = Random.Range(0.0f, 1000.0f);
            noiseOffsetY = Random.Range(0.0f, 1000.0f);
            noiseOffsetIntensity = Random.Range(0.0f, 1000.0f);
        }

        void Update() {
            if (lights == null || lights.Length == 0) {
                return;
            }

            float time = Time.time;
            Vector3 offset = SamplePositionOffset(time);
            float multiplier = SampleIntensityMultiplier(time);

            for (int i = 0; i < lights.Length; i++) {
                var light = lights[i];
                if (light == null) {
                    continue;
                }

                light.transform.localPosition = baselinePositions[i] + offset;
                light.intensity = baselineIntensities[i] * multiplier;
            }
        }

        private void CacheBaselines() {
            baselinePositions = new Vector3[lights.Length];
            baselineIntensities = new float[lights.Length];

            for (int i = 0; i < lights.Length; i++) {
                var light = lights[i];
                if (light == null) {
                    continue;
                }

                baselinePositions[i] = light.transform.localPosition;
                baselineIntensities[i] = light.intensity;
            }
        }

        private Vector3 SamplePositionOffset(float time) {
            float t = time * shakeFrequency;
            // Perlin returns 0..1; remap to -1..1 so the offset is centered on baseline.
            float x = (Mathf.PerlinNoise(noiseOffsetX, t) - 0.5f) * 2.0f;
            float y = (Mathf.PerlinNoise(noiseOffsetY, t) - 0.5f) * 2.0f;
            return new Vector3(x * shakeAmplitude.x, y * shakeAmplitude.y, 0.0f);
        }

        private float SampleIntensityMultiplier(float time) {
            float n = (Mathf.PerlinNoise(noiseOffsetIntensity, time * flickerFrequency) - 0.5f) * 2.0f;
            float multiplier = 1.0f + n * intensityVariation;

            if (enableBursts) {
                multiplier += UpdateBurst() * burstIntensityBonus;
            }

            return multiplier;
        }

        // Returns the burst envelope in 0..1: 1 right after a burst triggers,
        // decaying linearly to 0 over burstDuration. New bursts can only start
        // once the previous one has fully decayed.
        private float UpdateBurst() {
            float dt = Time.deltaTime;

            if (burstRemaining > 0.0f) {
                burstRemaining -= dt;
                if (burstRemaining < 0.0f) {
                    burstRemaining = 0.0f;
                }
            } else if (burstChancePerSecond > 0.0f && burstDuration > 0.0f) {
                // chance-per-second * dt approximates a Poisson process so the
                // average burst rate stays consistent across frame rates.
                if (Random.value < burstChancePerSecond * dt) {
                    burstRemaining = burstDuration;
                }
            }

            if (burstDuration <= 0.0f) {
                return 0.0f;
            }

            return burstRemaining / burstDuration;
        }
    }
}