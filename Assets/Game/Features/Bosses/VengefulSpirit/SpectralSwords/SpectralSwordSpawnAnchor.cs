using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Features.Bosses.VengefulSpirit.SpectralSwords {
    /// <summary>
    /// Self-contained spectral-sword cast point. Each anchor owns:
    ///   - its base position (the GameObject's transform),
    ///   - the pattern that describes the wave it fires,
    ///   - the sword prefab to spawn,
    ///   - the coroutine that runs the wave.
    ///
    /// Drop several anchors into a scene at fixed positions, parent one under the player to
    /// follow them, or parent one under the camera for "above the visible area" behaviour.
    /// The boss (or any caller) picks an anchor and invokes <see cref="Cast"/> — anchors are
    /// independent, so multiple can fire at once if desired.
    /// </summary>
    public class SpectralSwordSpawnAnchor : MonoBehaviour {
        [SerializeField]
        private GameObject swordPrefab;

        [Tooltip("Pattern fired by this anchor. Both explicit and line variants are accepted.")]
        [SerializeField]
        private SpectralSwordPattern pattern;

        private Coroutine activeCast;

        public bool IsCasting => activeCast != null;
        public SpectralSwordPattern Pattern => pattern;
        public Vector3 Position => transform.position;

        public void Cast(Action onComplete) {
            if (activeCast != null) {
                return;
            }
            activeCast = StartCoroutine(RunWave(onComplete));
        }

        public void CancelActiveCast() {
            if (activeCast != null) {
                StopCoroutine(activeCast);
                activeCast = null;
            }
        }

        private IEnumerator RunWave(Action onComplete) {
            if (pattern == null || swordPrefab == null) {
                activeCast = null;
                onComplete?.Invoke();
                yield break;
            }

            IReadOnlyList<SpectralSwordPattern.Entry> entries = pattern.Entries;
            if (entries == null) {
                activeCast = null;
                onComplete?.Invoke();
                yield break;
            }

            // Snapshot the anchor's position when the wave starts so a moving anchor (e.g. one
            // parented to the player) doesn't drag already-queued spawns around mid-cast.
            Vector3 origin = transform.position;

            for (int i = 0; i < entries.Count; i++) {
                SpectralSwordPattern.Entry entry = entries[i];
                if (entry.spawnDelay > 0f) {
                    yield return new WaitForSeconds(entry.spawnDelay);
                }
                SpawnOne(origin, entry);
            }

            activeCast = null;
            onComplete?.Invoke();
        }

        private void SpawnOne(Vector3 origin, SpectralSwordPattern.Entry entry) {
            Vector3 spawnPos = ComputeSpawnPosition(origin, entry);

            GameObject go = Instantiate(swordPrefab, spawnPos, Quaternion.identity);
            SpectralSword sword = go.GetComponent<SpectralSword>();
            if (sword == null) {
                return;
            }

            float speed = entry.flightSpeedOverride > 0f ? entry.flightSpeedOverride : pattern.DefaultFlightSpeed;
            sword.Configure(
                entry.flightDirection,
                speed,
                entry.telegraphTime,
                pattern.DefaultMaxTravelDistance,
                pattern.DefaultLifetime);
        }

        private static Vector3 ComputeSpawnPosition(Vector3 origin, SpectralSwordPattern.Entry entry) {
            return new Vector3(
                origin.x + entry.lateralOffset,
                origin.y + entry.verticalOffset,
                0f);
        }

#if UNITY_EDITOR
        // Always-visible marker plus a wave preview when this anchor's pattern asset is the
        // selected object in the editor — so opening a pattern in the project view lights up
        // every anchor that uses it.
        private void OnDrawGizmos() {
            DrawAnchorMarker();
            if (IsLinkedPatternSelected()) {
                DrawWavePreview();
            }
        }

        // Full wave preview when the anchor itself is selected.
        private void OnDrawGizmosSelected() {
            DrawWavePreview();
        }

        private bool IsLinkedPatternSelected() {
            return pattern != null && UnityEditor.Selection.activeObject == pattern;
        }

        private void DrawAnchorMarker() {
            Gizmos.color = new Color(0.4f, 1f, 1f, 0.9f);
            Vector3 p = transform.position;
            Gizmos.DrawWireSphere(p, 0.15f);
            Gizmos.DrawLine(p + Vector3.left * 0.35f, p + Vector3.right * 0.35f);
            Gizmos.DrawLine(p + Vector3.up * 0.35f, p + Vector3.down * 0.35f);

            UnityEditor.Handles.color = new Color(0.4f, 1f, 1f, 0.9f);
            UnityEditor.Handles.Label(p + new Vector3(0.4f, 0.25f, 0f), $"Sword Anchor: {name}");
        }

        private void DrawWavePreview() {
            if (pattern == null) {
                return;
            }

            IReadOnlyList<SpectralSwordPattern.Entry> entries = pattern.Entries;
            if (entries == null || entries.Count == 0) {
                return;
            }

            Vector3 origin = transform.position;
            float maxDistance = pattern.DefaultMaxTravelDistance;

            for (int i = 0; i < entries.Count; i++) {
                SpectralSwordPattern.Entry entry = entries[i];
                Vector3 spawnPos = ComputeSpawnPosition(origin, entry);

                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(spawnPos, 0.18f);

                Vector2 dir = entry.flightDirection.sqrMagnitude > 0f
                    ? entry.flightDirection.normalized
                    : Vector2.down;
                Vector3 endPos = spawnPos + (Vector3)(dir * maxDistance);

                Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.85f);
                Gizmos.DrawLine(spawnPos, endPos);

                UnityEditor.Handles.color = Color.cyan;
                UnityEditor.Handles.Label(spawnPos + new Vector3(0.2f, 0.2f, 0f), $"#{i}  d={entry.spawnDelay:0.##}s");
            }
        }
#endif
    }
}
