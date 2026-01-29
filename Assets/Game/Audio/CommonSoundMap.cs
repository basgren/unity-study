using Core.Audio;
using UnityEngine;

namespace Game.Audio {
    [CreateAssetMenu(fileName = "MoveSoundProfile", menuName = "Audio/Profiles/MoveSoundProfile")]
    public class MoveSoundProfile : ScriptableObject {
        [SerializeField]
        private AudioCue step;
        
        [SerializeField]
        private AudioCue jump;
        
        [SerializeField]
        private AudioCue landing;

        public AudioCue Step => step;
        public AudioCue Jump => jump;
        public AudioCue Landing => landing;
    }
    
    [CreateAssetMenu(fileName = "HeroAttackSoundProfile", menuName = "Audio/Profiles/HeroAttackSoundProfile")]
    public class HeroAttackSoundProfile : ScriptableObject {
        [SerializeField]
        private AudioCue attack;
        
        [SerializeField]
        private AudioCue throwSword;
        
        public AudioCue Attack => attack;
        public AudioCue ThrowSword => throwSword;
    }
    
    [CreateAssetMenu(fileName = "HurtSoundProfile", menuName = "Audio/Profiles/HurtSoundProfile")]
    public class HurtSoundProfile : ScriptableObject {
        [SerializeField]
        private AudioCue hit;

        public AudioCue Hit => hit;
    }
}
