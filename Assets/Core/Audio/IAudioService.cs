using UnityEngine;

namespace Core.Audio {
    /// <summary>
    /// Abstraction for playing audio cues.
    /// Use this from gameplay code instead of talking to AudioSource directly.
    /// </summary>
    public interface IAudioService {
        /// <summary>
        /// Plays a cue as a 2D sound (UI, footsteps in a 2D platformer, etc.).
        /// </summary>
        void Play2D(AudioCue cue);

        /// <summary>
        /// Plays a cue at a world position (3D/spatial playback).
        /// </summary>
        void PlayAt(AudioCue cue, Vector3 worldPosition);

        IAudioLoopHandle PlayLoopFollow(AudioCue cue, Transform follow, bool is3D = true);

        IAudioLoopHandle PlayLoopAt(AudioCue cue, Vector3 worldPosition, bool is3D = true);
    }
}
