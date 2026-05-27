using System.Collections;
using Game.Features.Bosses._Shared;
using UnityEngine;

namespace Game.Features.Bosses.StoneGolem.Actions {
    /// <summary>
    /// Ground Hit: the golem collapses into a ball (the "isImmune" animation just shows entering /
    /// exiting the ball — despite the name the golem stays vulnerable), then raises up and slams back
    /// down; on impact a burst of falling stones rains over the arena. The whole maneuver is timer-
    /// driven (no animation events needed): collapse → raise → hang → slam → rain stones → recover →
    /// un-ball. The stones spawn at random X across <see cref="spawnArea"/>'s world bounds — that
    /// collider is a room/scene object, NOT a child of the golem, so the rain stays fixed to the room.
    /// </summary>
    public class StoneGolemGroundHitAction : EnemyAction {
        [Header("References")]
        [SerializeField]
        private StoneGolem golem;

        [SerializeField]
        private Animator animator;

        [SerializeField]
        [Tooltip("Bool parameter that plays the ball-up / un-ball animation. Set true on start, false " +
                 "on recover. Despite the name the golem is NOT damage-immune during the slam.")]
        private string animBoolKey = "isImmune";

        [Header("Slam motion")]
        [SerializeField]
        [Tooltip("Time to collapse into the ball before raising (covers the ball-up animation).")]
        private float collapseTime = 1f;

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
        [Tooltip("Time of the downward slam (small = fast/furious).")]
        private float slamTime = 0.15f;

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

        private int animBoolHash;

        private void Awake() {
            animBoolHash = Animator.StringToHash(animBoolKey);
        }

        protected override void OnBegin() {
            SetImmuneAnim(true);
            StartCoroutine(SlamRoutine());
        }

        protected override void OnEnd() {
            // Always un-ball and restore gravity, including a mid-air cancel (e.g. death during the slam).
            SetImmuneAnim(false);
            if (golem != null) {
                golem.SetGravityActive(true);
            }
        }

        private IEnumerator SlamRoutine() {
            yield return new WaitForSeconds(collapseTime);

            // Scripted vertical raise + slam: gravity off so the move is predictable, back on after.
            if (golem != null) {
                golem.SetGravityActive(false);
                yield return golem.MoveByVertical(raiseHeight, raiseTime);

                if (hangTime > 0f) {
                    yield return new WaitForSeconds(hangTime);
                }

                yield return golem.MoveByVertical(-raiseHeight, slamTime);
                golem.SetGravityActive(true);
            }

            yield return SpawnStonesRoutine();

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

        private void SetImmuneAnim(bool value) {
            if (animator != null) {
                animator.SetBool(animBoolHash, value);
            }
        }
    }
}
