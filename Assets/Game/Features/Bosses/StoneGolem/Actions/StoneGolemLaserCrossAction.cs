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
    /// Reuses <c>StoneGolemBeam</c> in externally-aimed mode (<c>SetManagedAim</c>) so the pivot drives
    /// their rotation together rather than each beam sweeping on its own.
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
        [Tooltip("Room-centre point the golem flies to before firing.")]
        private Transform centerPoint;

        [Header("Cross")]
        [SerializeField]
        [Tooltip("Number of beams spread evenly around the pivot.")]
        private int beamCount = 4;

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

        private void Awake() {
            animBoolHash = Animator.StringToHash(animBoolKey);
        }

        protected override void OnBegin() {
            StartCoroutine(CrossRoutine());
        }

        protected override void OnEnd() {
            SetCastAnim(false);
            if (golem != null) {
                golem.SetGravityActive(true);
            }

            CleanupBeams();
        }

        private IEnumerator CrossRoutine() {
            Vector3 startPos = golem != null ? golem.transform.position : Vector3.zero;
            float rotationDuration = rotationSpeed > 0f ? Mathf.Abs(totalRotationDeg) / rotationSpeed : 0f;

            SetCastAnim(true);

            if (golem != null) {
                golem.SetGravityActive(false);
                if (centerPoint != null) {
                    yield return golem.MoveTo(centerPoint.position, flyToCenterTime);
                }
            }

            SpawnCross(rotationDuration);

            // Rotate the whole cross by totalRotationDeg at rotationSpeed.
            float t = 0f;
            while (t < rotationDuration) {
                t += Time.deltaTime;
                float k = rotationDuration > 0f ? Mathf.Clamp01(t / rotationDuration) : 1f;
                if (pivot != null) {
                    pivot.transform.localRotation = Quaternion.Euler(0f, 0f, totalRotationDeg * k);
                }

                yield return null;
            }

            // The beams finish on their own loop timers (~rotationDuration); hold while they fade.
            if (standAfterTime > 0f) {
                yield return new WaitForSeconds(standAfterTime);
            }

            CleanupBeams();

            if (golem != null) {
                yield return golem.MoveTo(startPos, flyDownTime);
                golem.SetGravityActive(true);
            }

            SetCastAnim(false);
            Complete();
        }

        private void SpawnCross(float loopDuration) {
            if (beamPrefab == null || golem == null) {
                return;
            }

            pivot = new GameObject("LaserCrossPivot");
            pivot.transform.position = golem.transform.position;

            int count = Mathf.Max(1, beamCount);
            float step = 360f / count;
            for (int i = 0; i < count; i++) {
                GameObject instance = Instantiate(beamPrefab, pivot.transform.position, Quaternion.identity, pivot.transform);
                StoneGolemBeam beam = instance.GetComponent<StoneGolemBeam>();
                if (beam == null) {
                    Destroy(instance);
                    continue;
                }

                beam.Initialize(loopDuration);
                beam.SetManagedAim(step * i);
                beams.Add(beam);
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
            if (animator != null) {
                animator.SetBool(animBoolHash, value);
            }
        }
    }
}
