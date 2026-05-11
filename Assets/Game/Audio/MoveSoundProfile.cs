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
}
