using Core.Audio;
using UnityEngine;

namespace Game.Audio {
    [CreateAssetMenu(fileName = "HurtSoundProfile", menuName = "Audio/Profiles/HurtSoundProfile")]
    public class HurtSoundProfile : ScriptableObject {
        [SerializeField]
        private AudioCue hit;

        public AudioCue Hit => hit;
    }
}
