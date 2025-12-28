using PixelCrew.Player;
using UnityEngine;

namespace Prefabs.Hero.Animations.Armed {
    public class AttackStateBehavior : StateMachineBehaviour {
        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
            // We don't allow jumping when attack is in progress, but if player falls from the edge while
            // attacking, animation will be interrupted and the last `FinishAttack()` event won't be
            // called. That's why we need to finish attack here.
            var pc = animator.GetComponentInParent<PlayerController>();

            // TODO: [BG] Rework in more elegant way using FSM. Currently we have to cancel attack here from
            //   animation state.
            if (pc != null) {
                pc.CancelAttack();
            }
        }
    }
}
