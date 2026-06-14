using Core.Audio;
using UnityEngine;

namespace Game.Core.Audio {
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
        /// Plays a 2D sound and returns a handle that can stop it early.
        /// The sound does not loop — it finishes naturally unless stopped.
        /// </summary>
        IAudioLoopHandle Play2DTracked(AudioCue cue);

        /// <summary>
        /// Plays a cue at a world position (3D/spatial playback).
        /// </summary>
        void PlayAt(AudioCue cue, Vector3 worldPosition);

        IAudioLoopHandle PlayLoopFollow(AudioCue cue, Transform follow, bool is3D = true);

        IAudioLoopHandle PlayLoopAt(AudioCue cue, Vector3 worldPosition, bool is3D = true);

        /// <summary>
        /// Assigns the current level's looped music cue and starts playing it. Typically
        /// called by a per-scene component (e.g. <c>LevelEntryPoint</c>) in <c>Start</c>.
        /// Stopping an existing track is handled internally so each level has a single
        /// active music handle.
        /// </summary>
        void SetLevelMusic(AudioCue cue);

        /// <summary>
        /// Pushes a temporary music override (e.g. a music zone the player just entered). The most
        /// recently pushed override is the track that plays; remove it with
        /// <see cref="RemoveMusicOverride"/> to fall back to the next override or the level music.
        /// A null cue is a deliberate silence override.
        /// </summary>
        void PushMusicOverride(AudioCue cue);

        /// <summary>
        /// Removes a previously pushed music override and transitions to whatever should play next
        /// (a lower override, the assigned level music, or silence when there is no level music).
        /// </summary>
        void RemoveMusicOverride(AudioCue cue);

        /// <summary>
        /// Stops the current level music (with optional fade-out) and forgets the assigned cue and
        /// any active zone overrides. Called when the level is torn down.
        /// </summary>
        void ClearLevelMusic(float fadeOutSeconds = 0.25f);

        /// <summary>
        /// Ducks the currently assigned level music without clearing the assignment.
        /// Pair with <see cref="StartLevelMusic"/> to bring the same cue back.
        /// Intended for events like hero death that want to silence the track temporarily.
        /// </summary>
        void StopLevelMusic(float fadeOutSeconds = 0.25f);

        /// <summary>
        /// Re-starts the currently assigned level music from the beginning. No-op when
        /// no cue has been assigned.
        /// </summary>
        void StartLevelMusic();
    }
}
