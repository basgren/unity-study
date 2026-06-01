using System.Collections;
using Game.Features.Bosses._Shared;
using Game.Features.Bosses.StoneGolem.Beam;
using UnityEngine;

namespace Game.Features.Bosses.StoneGolem.Actions {
    /// <summary>
    /// Casts a sustained beam. Drives the golem's animator via a single bool parameter (true on
    /// start, false on cleanup) and spawns the beam on the animation's effect frame (<see cref="Do"/>).
    /// The action owns the beam's aim and timing: it points the beam at <see cref="startAngleDeg"/>,
    /// and once the beam's loop (damage) phase begins it sweeps the aim up by <see cref="sweepArcDeg"/>
    /// over <see cref="sweepTime"/>, holds <see cref="loopDuration"/> seconds total, then tells the
    /// beam to finish. It completes when the beam raises <see cref="StoneGolemBeam.Finished"/>,
    /// despawning the beam and clearing the bool.
    /// </summary>
    public class StoneGolemBeamShootAction : EnemyAction {
        [Header("References")]
        [SerializeField]
        private Animator animator;

        [SerializeField]
        [Tooltip("Beam prefab carrying a StoneGolemBeam.")]
        private GameObject beamPrefab;

        [SerializeField]
        private Transform spawnPoint;

        [Header("Animation")]
        [SerializeField]
        [Tooltip("Bool parameter on the golem animator. Set true on action start, false when the beam finishes.")]
        private string animBoolKey = "isCastingBeam";

        [Header("Aim sweep")]
        
        [SerializeField]
        [Tooltip("Time in seconds after beam fires and sweep starts")]
        private float sweepDelay = 1f;
        
        [SerializeField]
        [Tooltip("Local Z angle (degrees) the beam starts at, relative to the golem's facing. " +
                 "Negative points below horizontal. Default -45 = 45° down.")]
        private float startAngleDeg = -45f;

        [SerializeField]
        [Tooltip("Degrees the beam rotates UP from the start angle across the loop (damage) phase. " +
                 "Default 90 ends 45° above horizontal.")]
        private float sweepArcDeg = 90f;

        [SerializeField]
        [Tooltip("Seconds to sweep the full arc once the loop phase begins. Should be <= Loop Duration; " +
                 "any remaining loop time holds the beam at the final angle.")]
        private float sweepTime = 2f;

        [Header("Loop timing")]
        [SerializeField]
        [Tooltip("Seconds the beam stays in its loop (damage) phase before being told to finish. " +
                 "Keep this plus the start/finish clip lengths under Max Duration (the safety cap).")]
        private float loopDuration = 3f;

        private int animBoolHash;
        private StoneGolemBeam activeBeam;

        private void Awake() {
            animBoolHash = Animator.StringToHash(animBoolKey);
        }

        protected override void OnBegin() {
            if (animator != null) {
                animator.SetBool(animBoolHash, true);
            }
        }

        public override void Do() {
            if (beamPrefab == null || spawnPoint == null) {
                // Wiring missing — the base safety timeout will still end the action cleanly.
                return;
            }

            // Parent to spawnPoint so the beam tracks the golem if it walks during the cast.
            // (Projectiles intentionally do NOT do this — they must hold a world-space trajectory.)
            GameObject instance = Instantiate(beamPrefab, spawnPoint.position, spawnPoint.rotation, spawnPoint);
            activeBeam = instance.GetComponent<StoneGolemBeam>();
            if (activeBeam == null) {
                Destroy(instance);
                return;
            }

            // Point at the start angle before the first render so the buildup clip already aims right.
            activeBeam.SetAim(startAngleDeg);
            activeBeam.LoopStarted += OnBeamLoopStarted;
            activeBeam.Finished += OnBeamFinished;
        }

        // The beam reports when its loop (damage) phase begins; from here the action sweeps the aim
        // and counts the loop duration, then tells the beam to finish.
        private void OnBeamLoopStarted() {
            StartCoroutine(LoopRoutine());
        }

        private IEnumerator LoopRoutine() {
            float elapsed = 0f;
            
            yield return new WaitForSeconds(sweepDelay);
            
            while (elapsed < loopDuration) {
                elapsed += Time.deltaTime;
                // Sweep up over sweepTime, then hold at the final angle for the rest of the loop.
                float t = sweepTime > 0f ? Mathf.Clamp01(elapsed / sweepTime) : 1f;
                if (activeBeam != null) {
                    activeBeam.SetAim(startAngleDeg + sweepArcDeg * t);
                }

                yield return null;
            }

            if (activeBeam != null) {
                activeBeam.PlayFinish();
            }
        }

        private void OnBeamFinished() {
            Complete();
        }

        protected override void OnEnd() {
            if (activeBeam != null) {
                activeBeam.LoopStarted -= OnBeamLoopStarted;
                activeBeam.Finished -= OnBeamFinished;
                Destroy(activeBeam.gameObject);
                activeBeam = null;
            }

            // Safety: clear the bool on every end path (natural complete, cancel-on-death, safety
            // timeout) so the animator can never stay stuck in the cast pose.
            if (animator != null) {
                animator.SetBool(animBoolHash, false);
            }
        }
    }
}
