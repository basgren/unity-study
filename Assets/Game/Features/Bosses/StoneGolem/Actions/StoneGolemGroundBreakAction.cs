using System.Collections;
using Game.Core.Bootstrap;
using Game.Features.Bosses._Shared;
using Game.Features.Characters.Hero;
using Game.Features.Hero;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Features.Bosses.StoneGolem.Actions {
    /// <summary>
    /// Phase-transition cutscene (the empowered Ground Hit). One-shot, run by <c>StoneGolemAI</c> at
    /// 50% HP via its <c>phaseTransitionAction</c> hook — never rolled as a normal attack. Timeline:
    /// walk to the break point on level 1 (velocity-based) → collapse into ball form → rise to the top of the view →
    /// shake with growing violence (all directions) → dramatic pause → physics slam
    /// (<see cref="StoneGolem.SlamToGround"/>) down onto the still-intact floor and shatter it.
    /// On impact the <see cref="breakObjects"/> (level-1 floor tilemap layer, the platforms, the
    /// phase-1 hook anchor) are disabled and debris is spawned; player control is locked and both the
    /// golem (gravity restored) and the player fall to the lower level. The camera confiner swap that
    /// reveals the lower level is handled in-scene, not here.
    /// </summary>
    public class StoneGolemGroundBreakAction : EnemyAction {
        [Header("References")]
        [SerializeField]
        private StoneGolem golem;

        [SerializeField]
        [Tooltip("Optional point on level 1 the golem walks to before the break. If empty, it breaks in place.")]
        private Transform breakPoint;

        [Header("Raise / shake / slam")]
        [SerializeField]
        [Tooltip("Height the golem rises to (upper part of the view, still visible).")]
        private float raiseHeight = 6f;

        [SerializeField]
        private float raiseTime = 1.5f;

        [SerializeField]
        [Tooltip("Duration of the growing shake at the top.")]
        private float shakeDuration = 3f;

        [SerializeField]
        private float shakeAmplitude = 0.25f;

        [SerializeField]
        private float shakeFrequency = 18f;

        [SerializeField]
        [Tooltip("Dramatic pause at the top before the slam.")]
        private float pauseTime = 1f;

        [SerializeField]
        [Tooltip("Slam tuning for the floor break: use a bigger gravity scale than Ground Hit for a " +
                 "faster, more furious fall. Shake/sound fire on impact, just before the floor shatters.")]
        private StoneGolem.SlamSettings slamSettings = new StoneGolem.SlamSettings();

        [Header("Break")]
        [SerializeField]
        [Tooltip("Objects disabled on impact: the level-1 floor tilemap layer, the platforms, and the " +
                 "phase-1 hook anchor. All are SetActive(false) so the player has nowhere to stand.")]
        private GameObject[] breakObjects;

        [SerializeField]
        [Tooltip("Optional debris effect spawned at each broken object's position on impact.")]
        private GameObject breakEffectPrefab;

        [SerializeField]
        [Tooltip("Seconds player control stays locked while everyone falls to the lower level.")]
        private float dropLockTime = 1.5f;

        [SerializeField]
        [Tooltip("Fired the instant the floor shatters — wire the camera confiner swap, SFX, and any " +
                 "screen shake here so they fire exactly on impact.")]
        private UnityEvent onFloorBroken;

        protected override void OnBegin() {
            StartCoroutine(CutsceneRoutine());
        }

        /// <summary>
        /// Debug/setup helper: applies the END state of the cutscene at once — shatters the floor
        /// (disabling <see cref="breakObjects"/> and firing <see cref="onFloorBroken"/>, e.g. the
        /// camera confiner swap) and restores the golem's gravity so it drops to the lower level —
        /// WITHOUT playing the raise/shake/slam. Lets a phase-2 debug start drop straight into the
        /// broken arena. Does not run the action lifecycle (no Begin/Complete).
        /// </summary>
        public void ApplyBrokenStateImmediate() {
            BreakFloor();
            
            golem.SetGravityActive(true);
        }

        protected override void OnEnd() {
            // Defensive restore on any end path (including a mid-cutscene cancel/death). Gravity
            // first: SetImmune(false) no-ops when the golem never balled up (early cancel), and
            // the un-ball flow owns gravity itself otherwise. StopMoving kills residual walk
            // velocity if the action was force-completed mid-WalkTo (the maxDuration safety cap).
            golem.StopMoving();
            golem.SetGravityActive(true);
            golem.SetImmune(false);

            SetPlayerControl(true);
        }

        private IEnumerator CutsceneRoutine() {
            // Velocity-based walk to the break point (gravity on, same ground movement as a chase) —
            // no position lerp on the ground.
            yield return golem.WalkTo(breakPoint.position.x);
            
            // Balling up zeroes gravity, which also holds the golem for the scripted raise.
            golem.SetImmune(true);
            yield return golem.MoveByVertical(raiseHeight, raiseTime, easeOut: true);
            yield return golem.Shake(shakeDuration, shakeAmplitude, shakeFrequency);

            if (pauseTime > 0f) {
                yield return new WaitForSeconds(pauseTime);
            }

            // Physics slam onto the still-intact level-1 floor; impact fires this action's
            // shake and sound, then the floor shatters.
            yield return golem.SlamToGround(slamSettings);

            BreakFloor();

            // Both fall to the lower level: restore the golem's gravity, lock the player for the drop.
            golem.SetGravityActive(true);

            SetPlayerControl(false);

            if (dropLockTime > 0f) {
                yield return new WaitForSeconds(dropLockTime);
            }

            // Complete() runs OnEnd, which un-balls the golem and unlocks the player.
            Complete();
        }

        private void BreakFloor() {
            if (breakObjects == null) {
                return;
            }

            foreach (GameObject obj in breakObjects) {
                if (obj == null) {
                    continue;
                }

                if (breakEffectPrefab != null) {
                    Instantiate(breakEffectPrefab, obj.transform.position, Quaternion.identity);
                }

                obj.SetActive(false);
            }

            // After the floor is gone: let the scene react (confiner swap, SFX, screen shake).
            onFloorBroken?.Invoke();
        }

        private void SetPlayerControl(bool isEnabled) {
            PlayerController hero = G.Hero != null ? G.Hero.Controller : null;
            if (hero != null) {
                hero.SetControlsEnabled(isEnabled);
            }
        }

        private void OnDrawGizmos() {
            // Break point the golem walks to before the floor break — visible in edit mode for placement.
            if (breakPoint == null) {
                return;
            }

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(breakPoint.position, 0.3f);
            Gizmos.DrawLine(breakPoint.position + Vector3.down * 1.5f, breakPoint.position + Vector3.up * 1.5f);
        }
    }
}
