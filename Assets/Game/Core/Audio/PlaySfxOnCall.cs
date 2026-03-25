using System;
using System.Collections.Generic;
using Core.Services;
using UnityEngine;

namespace Core.Audio {

    [Serializable]
    public class AudioCueRef {
        public string id;
        public AudioCue cue;
    }

    /// <summary>
    /// Simple helper component that plays a configured AudioCue when asked.
    /// Useful for animation events, UI buttons, and simple triggers.
    /// </summary>
    public class PlaySfxOnCall : MonoBehaviour {
        [SerializeField]
        private AudioCue defaultCue;

        [SerializeField]
        public AudioCueRef[] cueRefs;
        
        [SerializeField]
        private bool playAs2D = true;

        /// <summary>
        /// Reference to an audio service. Inject via ServiceLocator or assign manually.
        /// </summary>
        private IAudioService audioService;

        private void Awake() {
            audioService = G.Audio;
        }
        
        /// <summary>
        /// Plays the configured AudioCue through the audio service.
        /// </summary>
        public void Play(string cueName = "") {
            var cue = GetCue(cueName);
            
            if (audioService == null || cue == null) {
                return;
            }
            
            if (playAs2D) {
                audioService.Play2D(cue);
            } else {
                audioService.PlayAt(cue, transform.position);
            }
        }

        private AudioCue GetCue(string cueName) {
            foreach (var c in cueRefs) {
                if (c.id == cueName) {
                    return c.cue;
                }
            }
            
            return defaultCue;
        }
    }
    
    
}
