using UnityEngine;
using UnityEngine.Audio;

namespace Core.Audio {
    /// <summary>
    /// AudioCue is a data asset that describes how a sound should be played:
    /// clip variations, volume/pitch randomization, mixer routing, and limiting rules.
    /// It does not play audio by itself; it is consumed by an audio service.
    /// </summary>
    [CreateAssetMenu(menuName = "Audio/Audio Cue", fileName = "AudioCue")]
    public class AudioCue : ScriptableObject {
        [Header("Clips")]
        [SerializeField]
        private AudioClip[] clips;
        
        [Header("Mixer")]
        [SerializeField]
        private AudioMixerGroup mixerGroup;

        [Header("Volume")]
        [Range(0f, 1f)]
        [SerializeField]
        private float volume = 1f;
        
        [Header("Pitch Randomization")]
        [SerializeField]
        private bool randomizePitch = true;

        [Min(0f)]
        [SerializeField]
        private float pitchMin = 0.95f;

        [Min(0f)]
        [SerializeField]
        private float pitchMax = 1.05f;
        
        [Header("Instance Limiting")]
        [Min(0)]
        [SerializeField]
        private int maxInstances = 0; // 0 = unlimited

        [Min(0f)]
        [SerializeField]
        private float cooldownSeconds = 0f; // per-cue throttle

        [Header("Spatial (3D only)")]
        [Range(0f, 1f)]
        [SerializeField]
        private float spatialBlend = 0f; // 0 = 2D, 1 = 3D

        [Min(0f)]
        [SerializeField]
        private float minDistance = 1f;

        [Min(0f)]
        [SerializeField]
        private float maxDistance = 30f;
        
        [Header("Distance Culling")]
        [SerializeField]
        private bool forceMaxDistance = false;

        /// <summary>
        /// Mixer group used to route this sound into an AudioMixer (optional).
        /// </summary>
        public AudioMixerGroup MixerGroup => mixerGroup;

        /// <summary>
        /// Base volume multiplier applied to the AudioSource.
        /// </summary>
        public float Volume => volume;

        /// <summary>
        /// If true, pitch will be randomized in [PitchMin..PitchMax] on each play.
        /// </summary>
        public bool RandomizePitch => randomizePitch;

        /// <summary>
        /// Minimum pitch value when randomization is enabled.
        /// </summary>
        public float PitchMin => pitchMin;

        /// <summary>
        /// Maximum pitch value when randomization is enabled.
        /// </summary>
        public float PitchMax => pitchMax;

        /// <summary>
        /// Maximum concurrent instances for this cue (0 = unlimited).
        /// </summary>
        public int MaxInstances => maxInstances;

        /// <summary>
        /// Minimum time between plays for this cue (per-cue cooldown).
        /// </summary>
        public float CooldownSeconds => cooldownSeconds;

        /// <summary>
        /// Spatial blend value used when playing in 3D mode (0 = 2D, 1 = fully 3D).
        /// </summary>
        public float SpatialBlend => spatialBlend;

        /// <summary>
        /// Min distance for 3D attenuation (only used when playing in 3D).
        /// </summary>
        public float MinDistance => minDistance;

        /// <summary>
        /// Max distance for 3D attenuation (only used when playing in 3D).
        /// </summary>
        public float MaxDistance => maxDistance;
        
        /// <summary>
        /// When set to true, MaxDistance will be used for all playbacks, even non-spatial. Works only
        /// when sound is played using `PlayAt()` method.
        /// </summary>
        public bool ForceMaxDistance => forceMaxDistance;
        
        /// <summary>
        /// Picks one clip from the variations list.
        /// If rng is provided, it is used for deterministic selection (tests/replays).
        /// Otherwise, UnityEngine.Random is used.
        /// </summary>
        public AudioClip PickClip(System.Random rng = null) {
            if (!HasClips()) {
                return null;
            }

            if (clips.Length == 1) {
                return clips[0];
            }

            var index = 0;

            if (rng != null) {
                index = rng.Next(0, clips.Length);
            } else {
                index = Random.Range(0, clips.Length);
            }

            return clips[index];
        }
        
        /// <summary>
        /// Returns true if this cue has at least one valid AudioClip assigned.
        /// </summary>
        public bool HasClips() {
            return clips != null && clips.Length > 0;
        }
        
        /// <summary>
        /// Returns a pitch value for playback.
        /// If randomization is disabled, returns 1.0f.
        /// If pitch range is invalid, returns PitchMin.
        /// </summary>
        public float PickPitch() {
            if (!randomizePitch) {
                return 1f;
            }

            if (pitchMax <= pitchMin) {
                return pitchMin;
            }

            return Random.Range(pitchMin, pitchMax);
        }
    }
}
