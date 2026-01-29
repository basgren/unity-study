using System.Collections.Generic;
using UnityEngine;

namespace Core.Audio {
    /// <summary>
    /// Central audio playback service with pooled AudioSources.
    /// Applies AudioCue settings and enforces per-cue limits (cooldown/max instances).
    /// </summary>
    public class AudioService : MonoBehaviour, IAudioService {
        [Header("Pool")]
        [Min(1)]
        [SerializeField]
        private int initialPoolSize = 16;

        [Min(1)]
        [SerializeField]
        private int maxPoolSize = 64;

        [Header("Routing")]
        [SerializeField]
        private Transform poolRoot;

        private readonly Queue<AudioSource> freeSources = new Queue<AudioSource>();
        private readonly HashSet<AudioSource> busySources = new HashSet<AudioSource>();

        private readonly Dictionary<AudioCue, int> playingCountByCue = new Dictionary<AudioCue, int>();
        private readonly Dictionary<AudioCue, float> lastPlayTimeByCue = new Dictionary<AudioCue, float>();

        /// <summary>
        /// Initializes the pool root and pre-creates a set of AudioSources.
        /// </summary>
        private void Awake() {
            if (poolRoot == null) {
                poolRoot = transform;
            }

            WarmupPool();
        }

        /// <summary>
        /// Plays a cue as a 2D sound.
        /// This respects cooldown and max instances rules in the AudioCue.
        /// </summary>
        public void Play2D(AudioCue cue) {
            if (!CanPlay(cue)) {
                return;
            }

            var source = GetSource();
            if (source == null) {
                return;
            }

            ConfigureSource(source, cue, is3D: false, worldPosition: Vector3.zero);
            PlayInternal(source, cue);
        }

        /// <summary>
        /// Plays a cue at a world position (spatial/3D).
        /// This respects cooldown and max instances rules in the AudioCue.
        /// </summary>
        public void PlayAt(AudioCue cue, Vector3 worldPosition) {
            if (!CanPlay(cue)) {
                return;
            }

            var source = GetSource();
            if (source == null) {
                return;
            }

            ConfigureSource(source, cue, is3D: true, worldPosition: worldPosition);
            PlayInternal(source, cue);
        }

        /// <summary>
        /// Reclaims finished AudioSources back into the pool.
        /// </summary>
        private void Update() {
            ReclaimFinishedSources();
        }

        /// <summary>
        /// Pre-creates a number of pooled AudioSources for faster first-time playback.
        /// </summary>
        private void WarmupPool() {
            for (var i = 0; i < initialPoolSize; i++) {
                var source = CreateSource();
                freeSources.Enqueue(source);
            }
        }

        /// <summary>
        /// Creates a new pooled AudioSource GameObject under poolRoot.
        /// </summary>
        private AudioSource CreateSource() {
            var go = new GameObject("PooledAudioSource");
            go.transform.SetParent(poolRoot, worldPositionStays: false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;

            return source;
        }

        /// <summary>
        /// Gets an AudioSource from the pool.
        /// If pool is empty and we are below maxPoolSize, a new source is created.
        /// Returns null if no source is available.
        /// </summary>
        private AudioSource GetSource() {
            if (freeSources.Count > 0) {
                var src = freeSources.Dequeue();
                busySources.Add(src);
                return src;
            }

            var currentTotal = freeSources.Count + busySources.Count;
            if (currentTotal < maxPoolSize) {
                var src = CreateSource();
                busySources.Add(src);
                return src;
            }

            return null;
        }

        /// <summary>
        /// Releases an AudioSource back to the pool and resets its state.
        /// </summary>
        private void ReleaseSource(AudioSource source) {
            if (source == null) {
                return;
            }

            source.Stop();
            source.clip = null;
            source.outputAudioMixerGroup = null;

            busySources.Remove(source);
            freeSources.Enqueue(source);
        }

        /// <summary>
        /// Finds AudioSources that finished playing and returns them to the pool.
        /// Also decrements per-cue instance counters.
        /// </summary>
        private void ReclaimFinishedSources() {
            if (busySources.Count == 0) {
                return;
            }

            // We cannot modify busySources while iterating over it.
            var toRelease = ListPool<AudioSource>.Get();

            foreach (var src in busySources) {
                if (src == null) {
                    toRelease.Add(src);
                    continue;
                }

                if (!src.isPlaying) {
                    toRelease.Add(src);
                }
            }

            for (var i = 0; i < toRelease.Count; i++) {
                var src = toRelease[i];
                var cue = GetTaggedCue(src);

                if (cue != null) {
                    DecrementPlaying(cue);
                }

                ClearTaggedCue(src);
                ReleaseSource(src);
            }

            ListPool<AudioSource>.Release(toRelease);
        }

        /// <summary>
        /// Checks whether a cue is allowed to play right now:
        /// - cue is not null and has clips
        /// - cooldown not violated
        /// - maxInstances not exceeded
        /// </summary>
        private bool CanPlay(AudioCue cue) {
            if (cue == null) {
                return false;
            }

            if (!cue.HasClips()) {
                return false;
            }

            var now = Time.unscaledTime;

            if (cue.CooldownSeconds > 0f) {
                if (lastPlayTimeByCue.TryGetValue(cue, out var lastTime)) {
                    if (now - lastTime < cue.CooldownSeconds) {
                        return false;
                    }
                }
            }

            if (cue.MaxInstances > 0) {
                playingCountByCue.TryGetValue(cue, out var count);
                if (count >= cue.MaxInstances) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Applies AudioCue settings to a source: clip, volume, pitch, mixer routing,
        /// and 2D/3D spatial settings depending on is3D flag.
        /// Also attaches a tag component so we can decrement instance counters later.
        /// </summary>
        private void ConfigureSource(AudioSource source, AudioCue cue, bool is3D, Vector3 worldPosition) {
            if (source == null || cue == null) {
                return;
            }

            var clip = cue.PickClip();
            if (clip == null) {
                return;
            }

            source.transform.position = worldPosition;
            source.clip = clip;
            source.volume = cue.Volume;
            source.pitch = cue.PickPitch();
            source.outputAudioMixerGroup = cue.MixerGroup;

            if (is3D) {
                source.spatialBlend = Mathf.Clamp01(cue.SpatialBlend);
                source.minDistance = cue.MinDistance;
                source.maxDistance = cue.MaxDistance;
            } else {
                source.spatialBlend = 0f;
            }

            SetTaggedCue(source, cue);
        }

        /// <summary>
        /// Performs the actual Play() call after updating counters and timestamps.
        /// </summary>
        private void PlayInternal(AudioSource source, AudioCue cue) {
            if (source == null || cue == null) {
                return;
            }

            IncrementPlaying(cue);
            lastPlayTimeByCue[cue] = Time.unscaledTime;

            source.Play();
        }

        /// <summary>
        /// Increments the number of currently playing instances for the cue.
        /// Used to enforce cue.MaxInstances.
        /// </summary>
        private void IncrementPlaying(AudioCue cue) {
            if (cue == null) {
                return;
            }

            playingCountByCue.TryGetValue(cue, out var count);
            playingCountByCue[cue] = count + 1;
        }

        /// <summary>
        /// Decrements the number of currently playing instances for the cue.
        /// Removes the dictionary entry when it reaches zero.
        /// </summary>
        private void DecrementPlaying(AudioCue cue) {
            if (cue == null) {
                return;
            }

            playingCountByCue.TryGetValue(cue, out var count);
            count--;

            if (count <= 0) {
                playingCountByCue.Remove(cue);
            } else {
                playingCountByCue[cue] = count;
            }
        }

        /// <summary>
        /// Gets the AudioCue stored on the AudioSource tag component.
        /// </summary>
        private AudioCue GetTaggedCue(AudioSource src) {
            if (src == null) {
                return null;
            }

            var tag = src.GetComponent<AudioSourceTag>();
            if (tag == null) {
                return null;
            }

            return tag.Cue;
        }

        /// <summary>
        /// Writes the AudioCue into the AudioSource tag component (creates one if missing).
        /// </summary>
        private void SetTaggedCue(AudioSource src, AudioCue cue) {
            if (src == null) {
                return;
            }

            var tag = src.GetComponent<AudioSourceTag>();
            if (tag == null) {
                tag = src.gameObject.AddComponent<AudioSourceTag>();
            }

            tag.Cue = cue;
        }

        /// <summary>
        /// Clears the AudioCue stored in the AudioSource tag component.
        /// This prevents stale references if the object is reused from the pool.
        /// </summary>
        private void ClearTaggedCue(AudioSource src) {
            if (src == null) {
                return;
            }

            var tag = src.GetComponent<AudioSourceTag>();
            if (tag == null) {
                return;
            }

            tag.Cue = null;
        }
    }

    /// <summary>
    /// Stores the last AudioCue used by a pooled AudioSource.
    /// This enables correct decrementing of per-cue instance counters when playback ends.
    /// </summary>
    public class AudioSourceTag : MonoBehaviour {
        public AudioCue Cue;
    }

    /// <summary>
    /// Simple list pooling utility to avoid allocations in per-frame code.
    /// </summary>
    public static class ListPool<T> {
        private static readonly Stack<List<T>> pool = new Stack<List<T>>();

        /// <summary>
        /// Returns a cleared list instance from the pool, or creates a new one.
        /// </summary>
        public static List<T> Get() {
            if (pool.Count > 0) {
                var list = pool.Pop();
                list.Clear();
                return list;
            }

            return new List<T>(32);
        }

        /// <summary>
        /// Clears the list and returns it to the pool for reuse.
        /// </summary>
        public static void Release(List<T> list) {
            if (list == null) {
                return;
            }

            list.Clear();
            pool.Push(list);
        }
    }
}
