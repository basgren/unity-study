using System;
using Core.Audio;
using UnityEngine;

namespace Game.Audio.Dialog {
    /// <summary>
    /// Centralized registry of dialog AudioCues.
    /// Resolves a (speaker, soundId) pair to an AudioCue using a three-tier fallback:
    /// per-line soundId match -> character default -> global default.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogSoundLibrary", menuName = "Audio/Profiles/Dialog/DialogSoundLibrary")]
    public class DialogSoundLibrary : ScriptableObject {
        [SerializeField]
        private AudioCue globalDefaultTalkSound;

        [SerializeField]
        private CharacterEntry[] characters;

        /// <summary>
        /// Returns the AudioCue for the given speaker and optional soundId.
        /// Falls back through: soundId match -> character default -> global default.
        /// </summary>
        public AudioCue Resolve(string speaker, string soundId) {
            var profile = FindProfile(speaker);

            if (profile != null) {
                var cue = profile.FindSound(soundId);
                if (cue != null) {
                    return cue;
                }

                if (profile.DefaultTalkSound != null) {
                    return profile.DefaultTalkSound;
                }
            }

            return globalDefaultTalkSound;
        }

        private CharacterSoundProfile FindProfile(string speaker) {
            if (characters == null || string.IsNullOrEmpty(speaker)) {
                return null;
            }

            for (int i = 0; i < characters.Length; i++) {
                if (string.Equals(characters[i].speaker, speaker, StringComparison.OrdinalIgnoreCase)) {
                    return characters[i].profile;
                }
            }

            return null;
        }

        [Serializable]
        public struct CharacterEntry {
            public string speaker;
            public CharacterSoundProfile profile;
        }
    }
}
