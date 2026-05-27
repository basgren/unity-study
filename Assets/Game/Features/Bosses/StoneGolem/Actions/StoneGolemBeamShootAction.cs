using Game.Features.Bosses._Shared;
using Game.Features.Bosses.StoneGolem.Beam;
using UnityEngine;

namespace Game.Features.Bosses.StoneGolem.Actions {
    /// <summary>
    /// Casts a sustained beam. Drives the golem's animator via a single bool parameter (true on
    /// start, false on cleanup), spawns the beam on the animation's effect frame (<see cref="Do"/>),
    /// and waits for <see cref="StoneGolemBeam.Finished"/> — the beam owns its own start / loop /
    /// finish timing and damage window. On finish the action despawns the beam, clears the bool,
    /// and completes.
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

            // Beam's loop duration tracks this action's MaxDuration so tuning lives in one place.
            activeBeam.Initialize(MaxDuration);
            activeBeam.Finished += OnBeamFinished;
        }

        private void OnBeamFinished() {
            Complete();
        }

        protected override void OnEnd() {
            if (activeBeam != null) {
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
