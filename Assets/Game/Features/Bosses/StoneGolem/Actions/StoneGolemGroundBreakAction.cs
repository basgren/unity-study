using System.Collections;
using Game.Core.Bootstrap;
using Game.Features.Bosses._Shared;
using Game.Features.Characters.Hero;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Features.Bosses.StoneGolem.Actions {
    /// <summary>
    /// Phase-transition cutscene (the empowered Ground Hit). One-shot, run by <c>StoneGolemAI</c> at
    /// 50% HP via its <c>phaseTransitionAction</c> hook — never rolled as a normal attack. Timeline:
    /// move to the break point on level 1 → collapse (isImmune) → rise to the top of the view → shake
    /// with growing violence → lift a little higher → dramatic pause → slam down and shatter the floor.
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
        private Animator animator;

        [SerializeField]
        [Tooltip("Bool that plays the ball-up / un-ball animation (shared with Ground Hit).")]
        private string animBoolKey = "isImmune";

        [SerializeField]
        [Tooltip("Optional point on level 1 the golem moves to before the break. If empty, it breaks in place.")]
        private Transform breakPoint;

        [Header("Approach")]
        [SerializeField]
        [Tooltip("Time to move to the break point.")]
        private float moveToPointTime = 1.5f;

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
        [Tooltip("Extra lift just before the slam.")]
        private float extraRaiseHeight = 1f;

        [SerializeField]
        private float extraRaiseTime = 0.5f;

        [SerializeField]
        [Tooltip("Dramatic pause at the top before the slam.")]
        private float pauseTime = 1f;

        [SerializeField]
        [Tooltip("Time of the furious downward slam back to the break height.")]
        private float slamTime = 0.3f;

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

        private int animBoolHash;

        private void Awake() {
            animBoolHash = Animator.StringToHash(animBoolKey);
        }

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
            if (golem != null) {
                golem.SetGravityActive(true);
            }
        }

        protected override void OnEnd() {
            // Defensive restore on any end path (including a mid-cutscene cancel/death).
            SetImmuneAnim(false);
            if (golem != null) {
                golem.SetGravityActive(true);
            }

            SetPlayerControl(true);
        }

        private IEnumerator CutsceneRoutine() {
            // Slam target Y = the break height (the floor level we descend back to before shattering it).
            float groundY = golem != null ? golem.transform.position.y : 0f;

            if (golem != null) {
                golem.SetGravityActive(false);
            }

            if (golem != null && breakPoint != null) {
                groundY = breakPoint.position.y;
                yield return golem.MoveTo(new Vector2(breakPoint.position.x, breakPoint.position.y), moveToPointTime);
            }

            SetImmuneAnim(true);

            if (golem != null) {
                yield return golem.MoveByVertical(raiseHeight, raiseTime);
                yield return golem.ShakeHorizontal(shakeDuration, shakeAmplitude, shakeFrequency);
                yield return golem.MoveByVertical(extraRaiseHeight, extraRaiseTime);
            }

            if (pauseTime > 0f) {
                yield return new WaitForSeconds(pauseTime);
            }

            if (golem != null) {
                yield return golem.MoveTo(new Vector2(golem.transform.position.x, groundY), slamTime);
            }

            BreakFloor();

            // Both fall to the lower level: restore the golem's gravity, lock the player for the drop.
            if (golem != null) {
                golem.SetGravityActive(true);
            }

            SetPlayerControl(false);

            if (dropLockTime > 0f) {
                yield return new WaitForSeconds(dropLockTime);
            }

            SetPlayerControl(true);
            SetImmuneAnim(false);
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

        private void SetImmuneAnim(bool value) {
            if (animator != null) {
                animator.SetBool(animBoolHash, value);
            }
        }

        private void SetPlayerControl(bool isEnabled) {
            PlayerController hero = G.Hero != null ? G.Hero.Controller : null;
            if (hero != null) {
                hero.SetControlsEnabled(isEnabled);
            }
        }
    }
}
