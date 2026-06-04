using System.Collections;
using Game.Features.Bosses._Shared;
using UnityEngine;

namespace Game.Features.Bosses.StoneGolem.Actions {
    /// <summary>
    /// Ground Hit: the golem collapses into its ball form (via <see cref="StoneGolem.SetImmune"/>,
    /// which swaps to the small ball collider), then performs <see cref="slamCount"/> raise + slam
    /// cycles with the shared physics slam (<see cref="StoneGolem.SlamToGround"/>) before un-balling,
    /// so the whole maneuver reads as one big action. Each impact starts a burst of falling stones
    /// that rains while the golem raises for the next slam. Timeline: collapse → (raise → hang →
    /// slam → stones) × slamCount → recover → un-ball. The stones spawn at random X across
    /// <see cref="spawnArea"/>'s world bounds — that collider is a room/scene object, NOT a child
    /// of the golem, so the rain stays fixed to the room.
    /// </summary>
    public class StoneGolemGroundHitAction : EnemyAction {
        [Header("References")]
        [SerializeField]
        private StoneGolem golem;

        [Header("Slam motion")]
        [SerializeField]
        [Tooltip("Time to collapse into the ball before raising (covers the ball-up animation).")]
        private float collapseTime = 1f;

        [SerializeField]
        [Tooltip("How many raise + slam cycles the golem performs before un-balling. Make sure Max " +
                 "Duration covers all of them.")]
        private int slamCount = 3;

        [SerializeField]
        [Tooltip("How high the golem rises before slamming.")]
        private float raiseHeight = 2f;

        [SerializeField]
        [Tooltip("Time to rise to the raised position.")]
        private float raiseTime = 0.5f;

        [SerializeField]
        [Tooltip("Dramatic pause at the top before the slam.")]
        private float hangTime = 0.2f;

        [SerializeField]
        [Tooltip("Slam tuning for this attack: fall gravity, camera shake, impact sound.")]
        private StoneGolem.SlamSettings slamSettings = new StoneGolem.SlamSettings();

        [SerializeField]
        [Tooltip("Settle time after the slam before the golem un-balls and the action completes.")]
        private float recoverTime = 0.4f;

        [Header("Falling stones")]
        [SerializeField]
        [Tooltip("Stone prefab dropped on slam impact (FallingStone + Damager).")]
        private GameObject fallingStonePrefab;

        [SerializeField]
        [Tooltip("Room/scene collider whose world bounds define where stones spawn: random X across it, " +
                 "dropped from the top edge. Must NOT be a child of the golem. A disabled BoxCollider2D " +
                 "spanning the room width works well.")]
        private Collider2D spawnArea;

        [SerializeField]
        [Tooltip("How many stones drop per slam.")]
        private int stoneCount = 5;

        [SerializeField]
        [Tooltip("Window over which the stones drop, for a rain feel rather than one burst. 0 = all at once.")]
        private float spawnDuration = 0.6f;

        protected override void OnBegin() {
            golem.SetImmune(true);
            StartCoroutine(SlamRoutine());
        }

        protected override void OnEnd() {
            // Always un-ball and restore gravity, including a mid-air cancel (e.g. death during the
            // slam). Gravity first: SetImmune(false) no-ops when already un-balled (cancel before
            // the ball-up landed), and the un-ball flow owns gravity itself otherwise.
            golem.SetGravityActive(true);
            golem.SetImmune(false);
        }

        private IEnumerator SlamRoutine() {
            yield return new WaitForSeconds(collapseTime);

            Coroutine lastSpawn = null;

            int count = Mathf.Max(1, slamCount);
            for (int i = 0; i < count; i++) {
                // The slam leaves gravity on, so re-disable it before each scripted raise.
                golem.SetGravityActive(false);
                yield return golem.MoveByVertical(raiseHeight, raiseTime, easeOut: true);

                if (hangTime > 0f) {
                    yield return new WaitForSeconds(hangTime);
                }

                yield return golem.SlamToGround(slamSettings);

                // Stones rain while the golem already raises for the next slam.
                lastSpawn = StartCoroutine(SpawnStonesRoutine());
            }

            // Let the final stone burst finish before completing — Complete() stops all
            // coroutines and would cut the rain short.
            if (lastSpawn != null) {
                yield return lastSpawn;
            }

            if (recoverTime > 0f) {
                yield return new WaitForSeconds(recoverTime);
            }

            Complete();
        }

        private IEnumerator SpawnStonesRoutine() {
            if (fallingStonePrefab == null || spawnArea == null) {
                yield break;
            }

            int count = Mathf.Max(1, stoneCount);
            // Spread the drops across spawnDuration, or burst them all at once when it is zero.
            float gap = count > 1 && spawnDuration > 0f ? spawnDuration / (count - 1) : 0f;

            Bounds area = spawnArea.bounds;
            for (int i = 0; i < count; i++) {
                float x = Random.Range(area.min.x, area.max.x);
                Vector3 pos = new Vector3(x, area.max.y, 0f);
                Instantiate(fallingStonePrefab, pos, Quaternion.identity);

                if (gap > 0f) {
                    yield return new WaitForSeconds(gap);
                }
            }
        }
    }
}
