using Game.Features.Characters._Shared;
using UnityEngine;

namespace Game.Features.Bosses.VengefulSpirit {
    static class VengefulSpiritAnimKeys {
        public static int IsCharging = Animator.StringToHash("isCharging");
        public static int OnAttack = Animator.StringToHash("onAttack");
        public static int OnDeath = Animator.StringToHash("onDeath");
        public static int OnHit = Animator.StringToHash("onHit");
    }

    public class VengefulSpirit : BaseCharacterController {
    }
}
