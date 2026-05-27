using System.Collections;
using Game.Features.Bosses._Shared;
using UnityEngine;

namespace Game.Features.Bosses.StoneGolem.Actions {
    /// <summary>
    /// Stone Wave (phase 2). The golem stands still and glows (vulnerable telegraph), then emits
    /// <see cref="waveCount"/> waves of ground spikes. Each wave sends two fronts out from the golem in
    /// both horizontal directions: a spike is dropped every <see cref="spikeSpacing"/> units, advancing
    /// at <see cref="travelSpeed"/>, until the front reaches a wall (a Ground-layer raycast). The
    /// <c>StoneSpikes</c> prefab plays its own rise/retract animation and self-despawns; this action
    /// only places them. Spikes rise at the probed ground Y, starting <see cref="spikeStartOffset"/>
    /// from the golem so they do not overlap it.
    /// </summary>
    public class StoneGolemStoneWaveAction : EnemyAction {
        [Header("References")]
        [SerializeField]
        private StoneGolem golem;

        [SerializeField]
        private Animator animator;

        [SerializeField]
        [Tooltip("Bool that plays the glow/flash telegraph. Set true while glowing, false at the end.")]
        private string glowBoolKey = "isGlowing";

        [SerializeField]
        [Tooltip("Spike prefab (StoneSpikes: self-animating + Damager). Set its animator to destroy on complete.")]
        private GameObject spikePrefab;

        [Header("Timing")]
        [SerializeField]
        [Tooltip("Glow/telegraph time before the first wave. The golem stands still and stays vulnerable.")]
        private float glowDuration = 2f;

        [SerializeField]
        [Tooltip("Number of spike waves.")]
        private int waveCount = 3;

        [SerializeField]
        [Tooltip("Pause between waves.")]
        private float waveSpacing = 1f;

        [Header("Wave shape")]
        [SerializeField]
        [Tooltip("How fast each front travels outward (units/second). Sets the spike-to-spike timing.")]
        private float travelSpeed = 5f;

        [SerializeField]
        [Tooltip("Horizontal distance between consecutive spikes in a front.")]
        private float spikeSpacing = 1f;

        [SerializeField]
        [Tooltip("Distance from the golem where the first spike of each front appears (so spikes don't overlap it).")]
        private float spikeStartOffset = 1.5f;

        [Header("Ground")]
        [SerializeField]
        [Tooltip("Ground/wall layers. Used to probe the spawn Y (raycast down) and to stop each front at a wall.")]
        private LayerMask groundLayerMask;

        [SerializeField]
        [Tooltip("Max front length if no wall is hit, and the down-probe distance for the ground Y.")]
        private float maxWaveRange = 30f;

        private int glowBoolHash;

        private void Awake() {
            glowBoolHash = Animator.StringToHash(glowBoolKey);
        }

        protected override void OnBegin() {
            if (golem != null) {
                golem.StopMoving();
            }

            StartCoroutine(WaveRoutine());
        }

        protected override void OnEnd() {
            SetGlow(false);
        }

        private IEnumerator WaveRoutine() {
            SetGlow(true);

            if (glowDuration > 0f) {
                yield return new WaitForSeconds(glowDuration);
            }

            int waves = Mathf.Max(1, waveCount);
            for (int w = 0; w < waves; w++) {
                yield return EmitWave();

                if (w < waves - 1 && waveSpacing > 0f) {
                    yield return new WaitForSeconds(waveSpacing);
                }
            }

            SetGlow(false);
            Complete();
        }

        private IEnumerator EmitWave() {
            Vector2 golemPos = golem != null ? (Vector2)golem.transform.position : Vector2.zero;

            // Probe the ground Y below the golem; cast the wall rays from just above it.
            RaycastHit2D down = Physics2D.Raycast(golemPos, Vector2.down, maxWaveRange, groundLayerMask);
            float groundY = down.collider != null ? down.point.y : golemPos.y;
            Vector2 rayOrigin = new Vector2(golemPos.x, groundY + 0.1f);

            float leftMax = WallDistance(rayOrigin, Vector2.left);
            float rightMax = WallDistance(rayOrigin, Vector2.right);
            float stepInterval = travelSpeed > 0f ? spikeSpacing / travelSpeed : 0f;

            float dist = spikeStartOffset;
            while (dist <= leftMax || dist <= rightMax) {
                if (dist <= leftMax) {
                    SpawnSpike(new Vector2(golemPos.x - dist, groundY));
                }

                if (dist <= rightMax) {
                    SpawnSpike(new Vector2(golemPos.x + dist, groundY));
                }

                dist += spikeSpacing;
                if (stepInterval > 0f) {
                    yield return new WaitForSeconds(stepInterval);
                }
            }
        }

        private float WallDistance(Vector2 origin, Vector2 dir) {
            RaycastHit2D hit = Physics2D.Raycast(origin, dir, maxWaveRange, groundLayerMask);
            return hit.collider != null ? hit.distance : maxWaveRange;
        }

        private void SpawnSpike(Vector2 pos) {
            if (spikePrefab != null) {
                Instantiate(spikePrefab, pos, Quaternion.identity);
            }
        }

        private void SetGlow(bool value) {
            if (animator != null) {
                animator.SetBool(glowBoolHash, value);
            }
        }
    }
}
