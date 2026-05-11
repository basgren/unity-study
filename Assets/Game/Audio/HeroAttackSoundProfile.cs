using Core.Audio;
using UnityEngine;

namespace Game.Audio {
    [CreateAssetMenu(fileName = "HeroAttackSoundProfile", menuName = "Audio/Profiles/HeroAttackSoundProfile")]
    public class HeroAttackSoundProfile : ScriptableObject {
        [SerializeField]
        private AudioCue attack;

        [SerializeField]
        private AudioCue throwSword;

        public AudioCue Attack => attack;
        public AudioCue ThrowSword => throwSword;
    }
}
