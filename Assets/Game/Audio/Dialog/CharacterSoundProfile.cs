using System;
using Core.Audio;
using UnityEngine;

namespace Game.Audio.Dialog {
    /// <summary>
    /// Groups all dialog AudioCues for a single character:
    /// a default talk sound and optional named sounds referenced by soundId.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterSoundProfile", menuName = "Audio/Profiles/Dialog/CharacterSoundProfile")]
    public class CharacterSoundProfile : ScriptableObject {
        [SerializeField]
        private AudioCue defaultTalkSound;

        [SerializeField]
        private SoundEntry[] sounds;

        public AudioCue DefaultTalkSound => defaultTalkSound;

        public AudioCue FindSound(string soundId) {
            if (string.IsNullOrEmpty(soundId) || sounds == null) {
                return null;
            }

            for (int i = 0; i < sounds.Length; i++) {
                if (sounds[i].id == soundId) {
                    return sounds[i].cue;
                }
            }

            return null;
        }

        [Serializable]
        public struct SoundEntry {
            public string id;
            public AudioCue cue;
        }
    }
}
