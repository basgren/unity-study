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
    
    [CreateAssetMenu(fileName = "AttackSoundProfile", menuName = "Audio/Profiles/AttackSoundProfile")]
    public class AttackSoundProfile : ScriptableObject {
        [SerializeField]
        private AudioCue attack;
        
        public AudioCue Attack => attack;
    }
    
    [CreateAssetMenu(fileName = "HurtSoundProfile", menuName = "Audio/Profiles/HurtSoundProfile")]
    public class HurtSoundProfile : ScriptableObject {
        [SerializeField]
        private AudioCue hit;

        public AudioCue Hit => hit;
    }
}
