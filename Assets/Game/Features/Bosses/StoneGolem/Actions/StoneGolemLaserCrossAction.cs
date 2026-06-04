using System.Collections;
using System.Collections.Generic;
using Game.Features.Bosses._Shared;
using Game.Features.Bosses.StoneGolem.Beam;
using UnityEngine;

namespace Game.Features.Bosses.StoneGolem.Actions {
    /// <summary>
    /// Laser Cross (phase 2). The golem flies to the room centre, spawns <see cref="beamCount"/> beams
    /// evenly around a shared pivot, then rotates the whole cross by <see cref="totalRotationDeg"/> at
    /// <see cref="rotationSpeed"/>; the beams finish, the golem holds briefly and flies back down.
    /// Reuses <c>StoneGolemBeam</c> as a passive visual (<c>SetAim</c> sets each beam's fixed offset)
    /// so the shared pivot drives their rotation together.
    ///
    /// NOTE: the full maneuver (fly + rotation + hold + descent) is long; set this action's
    /// <c>maxDuration</c> to cover it (or 0 to disable the safety cap), or it will force-complete early.
    /// </summary>
    public class StoneGolemLaserCrossAction : EnemyAction {
        [Header("References")]
        [SerializeField]
        private StoneGolem golem;

        [SerializeField]
        private Animator animator;

        [SerializeField]
        [Tooltip("Optional bool played while casting the cross.")]
        private string animBoolKey = "isCastingBeam";

        [SerializeField]
        [Tooltip("Beam prefab carrying a StoneGolemBeam (the same prefab as the single beam).")]
        private GameObject beamPrefab;

        [SerializeField]
        [Tooltip("Point the cross pivot is placed at (BeamSpawnPos). Falls back to the golem position when unset.")]
        private Transform spawnPoint;

        [SerializeField]
        [Tooltip("Room-centre point the golem flies to before firing.")]
        private Transform centerPoint;

        [Header("Cross")]
        [SerializeField]
        [Tooltip("Number of beams spread evenly around the pivot.")]
        private int beamCount = 3;

        [SerializeField]
        [Tooltip("Degrees the cross rotates in total before stopping.")]
        private float totalRotationDeg = 180f;

        [SerializeField]
        [Tooltip("Rotation speed in degrees per second.")]
        private float rotationSpeed = 30f;

        [Header("Timing")]
        [SerializeField]
        private float flyToCenterTime = 2f;

        [SerializeField]
        [Tooltip("Hold at the centre after the beams finish, before flying down.")]
        private float standAfterTime = 1f;

        [SerializeField]
        private float flyDownTime = 2f;

        private int animBoolHash;
        private readonly List<StoneGolemBeam> beams = new List<StoneGolemBeam>();
        private GameObject pivot;

        /// <summary>
        /// World position the cross is cast at (room centre). The AI walks the golem under this point
        /// before the action begins. Falls back to the golem's position when no centre is wired.
        /// </summary>
        public Vector2 CenterPosition => centerPoint != null
            ? centerPoint.position
            : golem.transform.position;

        private void Awake() {
            animBoolHash = Animator.StringToHash(animBoolKey);
        }

        protected override void OnBegin() {
            StartCoroutine(CrossRoutine());
        }

        protected override void OnEnd() {
            SetCastAnim(false);
            golem.SetGravityActive(true);
            CleanupBeams();
        }

        private IEnumerator CrossRoutine() {
            Vector3 startPos = golem.transform.position;
            float rotationDuration = rotationSpeed > 0f ? Mathf.Abs(totalRotationDeg) / rotationSpeed : 0f;

            SetCastAnim(true);

            golem.SetGravityActive(false);
            if (centerPoint != null) {
                yield return golem.MoveTo(centerPoint.position, flyToCenterTime);
            }

            SpawnCross();

            // Rotate the whole cross by totalRotationDeg at rotationSpeed.
            float t = 0f;
            while (t < rotationDuration) {
                t += Time.deltaTime;
                float k = rotationDuration > 0f ? Mathf.Clamp01(t / rotationDuration) : 1f;
                if (pivot != null) {
                    pivot.transform.localRotation = Quaternion.Euler(0f, 0f, totalRotationDeg * k);
                }

                // Re-assert every frame: unlike FlyTo/JumpTo the golem HOVERS here with nothing
                // pinning it, and a pending un-ball finishing mid-cast silently restores gravity
                // (SetImmuneStateEnabled) — without this the golem just falls out of the cross.
                golem.SetGravityActive(false);
                yield return null;
            }

            // Beams have no timer of their own — tell them to finish, then hold while they fade.
            FinishBeams();
            for (float hold = 0f; hold < standAfterTime; hold += Time.deltaTime) {
                // Same per-frame gravity hold as the rotation loop above.
                golem.SetGravityActive(false);
                yield return null;
            }

            CleanupBeams();

            yield return golem.MoveTo(startPos, flyDownTime);
            golem.SetGravityActive(true);

            SetCastAnim(false);
            Complete();
        }

        private void SpawnCross() {
            if (beamPrefab == null) {
                return;
            }

            // World-space pivot at BeamSpawnPos; intentionally NOT parented to it — the golem
            // flips facing via localScale.x, which would mirror the cross and its rotation.
            pivot = new GameObject("LaserCrossPivot");
            pivot.transform.position = spawnPoint != null ? spawnPoint.position : golem.transform.position;

            int count = Mathf.Max(1, beamCount);
            float step = 360f / count;
            for (int i = 0; i < count; i++) {
                GameObject instance = Instantiate(beamPrefab, pivot.transform.position, Quaternion.identity, pivot.transform);
                StoneGolemBeam beam = instance.GetComponent<StoneGolemBeam>();
                if (beam == null) {
                    Destroy(instance);
                    continue;
                }

                // Fixed offset around the pivot; the pivot's rotation sweeps them together.
                beam.SetAim(step * i);
                beams.Add(beam);
            }
        }

        private void FinishBeams() {
            for (int i = 0; i < beams.Count; i++) {
                if (beams[i] != null) {
                    beams[i].PlayFinish();
                }
            }
        }

        private void CleanupBeams() {
            for (int i = 0; i < beams.Count; i++) {
                if (beams[i] != null) {
                    Destroy(beams[i].gameObject);
                }
            }

            beams.Clear();

            if (pivot != null) {
                Destroy(pivot);
                pivot = null;
            }
        }

        private void SetCastAnim(bool value) {
            animator.SetBool(animBoolHash, value);
        }
    }
}
