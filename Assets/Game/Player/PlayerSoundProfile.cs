using Game.Audio;
using UnityEngine;

namespace Game.Player {
    [CreateAssetMenu(fileName = "PlayerSoundProfile", menuName = "Audio/Profiles/PlayerSoundProfile")]
    public class PlayerSoundProfile: ScriptableObject {
        [SerializeField]
        private MoveSoundProfile moveSoundProfile;
        
        [SerializeField]
        private HeroAttackSoundProfile attack;
        
        [SerializeField]
        private HurtSoundProfile hitSoundProfile;
        
        public MoveSoundProfile MoveSoundProfile => moveSoundProfile;
        public HeroAttackSoundProfile Attack => attack;
        public HurtSoundProfile HitSoundProfile => hitSoundProfile;
    }
}
