using Core.Services;
using UnityEngine;

namespace Core.Audio {
    /// <summary>
    /// Simple helper component that plays a configured AudioCue when asked.
    /// Useful for animation events, UI buttons, and simple triggers.
    /// </summary>
    public class PlaySfxOnCall : MonoBehaviour {
        [SerializeField]
        private AudioCue cue;

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
        public void Play() {
            if (audioService == null || cue == null) {
                return;
            }

            if (playAs2D) {
                audioService.Play2D(cue);
            } else {
                audioService.PlayAt(cue, transform.position);
            }
        }
    }
}
