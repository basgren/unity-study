using System;
using System.IO;
using Game.Configs;
using Game.Core.Bootstrap;
using UnityEngine;
using UnityEngine.Audio;

namespace Game.Core.Services {
    /// <summary>
    /// Persists game settings as JSON in Application.persistentDataPath.
    /// Loaded once at startup; saved when the options menu is closed.
    /// </summary>
    public class SettingsService : MonoBehaviour {
        private static readonly string FileName = "settings.json";
        private static readonly string MusicVolumeParam = "MusicVolume";
        private static readonly string SfxVolumeParam = "SfxVolume";
        private static readonly float MinDecibels = -80f;

        private AudioMixer mixer;

        // Per-channel ceilings (dB at slider value 10), from MainConfig. 0 = unity gain.
        private float musicMaxDecibels;
        private float sfxMaxDecibels;

        public GameSettings Current { get; private set; }

        private string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        private void Awake() {
            Current = Load();
        }

        /// <summary>
        /// Called after G.Config is available to grab the mixer reference.
        /// Volume is applied in Start because AudioMixer ignores SetFloat during Awake.
        /// </summary>
        public void Init() {
            if (G.Config != null) {
                mixer = G.Config.AudioMixer;
                musicMaxDecibels = G.Config.musicMaxDecibels;
                sfxMaxDecibels = G.Config.sfxMaxDecibels;
            }
        }

        private void Start() {
            ApplyVolume();
        }

        public void Save() {
            Save(Current);
        }

        public void Save(GameSettings settings) {
            Current = settings;

            try {
                var json = JsonUtility.ToJson(settings, prettyPrint: true);
                File.WriteAllText(FilePath, json);
            } catch (Exception e) {
                Debug.LogError($"Failed to save settings: {e.Message}");
            }
        }

        public GameSettings Load() {
            if (!File.Exists(FilePath)) {
                return new GameSettings();
            }

            try {
                var json = File.ReadAllText(FilePath);
                var settings = JsonUtility.FromJson<GameSettings>(json);
                return settings ?? new GameSettings();
            } catch (Exception e) {
                Debug.LogWarning($"Failed to load settings, using defaults: {e.Message}");
                return new GameSettings();
            }
        }

        /// <summary>
        /// Applies current volume settings to the AudioMixer.
        /// Slider values (0–10) are converted to decibels.
        /// </summary>
        public void ApplyVolume() {
            if (mixer == null) {
                return;
            }

            mixer.SetFloat(MusicVolumeParam, ToDecibels(Current.MusicVolume, musicMaxDecibels));
            mixer.SetFloat(SfxVolumeParam, ToDecibels(Current.SfxVolume, sfxMaxDecibels));
        }

        /// <summary>
        /// Converts a linear 0–10 slider value to decibels. 0 = silence (−80 dB); the maximum
        /// (slider value 10) maps to <paramref name="maxDecibels"/> instead of always 0 dB. The
        /// perceptual log curve is preserved and simply offset, so lowering a channel's ceiling
        /// makes the whole channel quieter without changing the slider's feel.
        /// </summary>
        private float ToDecibels(int value, float maxDecibels) {
            if (value <= 0) {
                return MinDecibels;
            }

            return Mathf.Log10(value / 10f) * 20f + maxDecibels;
        }
    }
}
