using System.Collections;
using Core.Audio;
using Game.Core.Bootstrap;
using Game.Features.Bosses._Shared;
using UnityEngine;

namespace Game.Features.Bosses.StoneGolem.Actions {
    /// <summary>
    /// Melee swing. Plays the melee animation; the damage window is opened and closed by relayed
    /// animation events (<see cref="Do"/> / <see cref="TearDown"/>), and the action ends on a
    /// code-owned hold timer so a missing close event cannot strand the golem mid-swing.
    /// </summary>
    public class StoneGolemMeleeAction : EnemyAction {
        [Header("Animation")]
        [SerializeField]
        private Animator animator;

        [SerializeField]
        [Tooltip("Trigger that starts the melee animation.")]
        private string attackTrigger = "onMeleeAttack";

        [Header("Damage")]
        [SerializeField]
        [Tooltip("Child object carrying the melee Damager. Toggled on only during the active hit frames.")]
        private GameObject meleeDamager;

        [Header("Timing")]
        [SerializeField]
        [Tooltip("How long the swing locks the golem before it can act again. Should roughly match the clip length.")]
        private float holdDuration = 1f;
        
        [Header("Sounds")]
        [SerializeField]
        private AudioCue meleeSound;

        private int attackTriggerHash;

        private void Awake() {
            attackTriggerHash = Animator.StringToHash(attackTrigger);
        }

        protected override void OnBegin() {
            SetDamagerActive(false);
            if (animator != null) {
                animator.SetTrigger(attackTriggerHash);
                G.Audio.Play2D(meleeSound);
            }

            StartCoroutine(HoldRoutine());
        }

        public override void Do() {
            SetDamagerActive(true);
        }

        public override void TearDown() {
            SetDamagerActive(false);
        }

        protected override void OnEnd() {
            // Defensive: never leave the hit window open past the action.
            SetDamagerActive(false);
        }

        private IEnumerator HoldRoutine() {
            yield return new WaitForSeconds(holdDuration);
            Complete();
        }

        private void SetDamagerActive(bool isActive) {
            if (meleeDamager != null) {
                meleeDamager.SetActive(isActive);
            }
        }
    }
}
