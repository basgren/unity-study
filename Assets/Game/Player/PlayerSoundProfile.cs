using Game.Audio;
using UnityEngine;

namespace Game.Player {
    [CreateAssetMenu(fileName = "PlayerSoundProfile", menuName = "Audio/Profiles/PlayerSoundProfile")]
    public class PlayerSoundProfile: ScriptableObject {
        [SerializeField]
        private MoveSoundProfile moveSoundProfile;
        
        [SerializeField]
        private AttackSoundProfile attackSoundProfile;
        
        [SerializeField]
        private HurtSoundProfile hitSoundProfile;
        
        public MoveSoundProfile MoveSoundProfile => moveSoundProfile;
        public AttackSoundProfile AttackSoundProfile => attackSoundProfile;
        public HurtSoundProfile HitSoundProfile => hitSoundProfile;
    }
}
