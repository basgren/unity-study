using System;
using System.IO;
using Game.Configs;
using UnityEngine;

namespace Game.Core.Services {
    /// <summary>
    /// Persists game settings as JSON in Application.persistentDataPath.
    /// Loaded once at startup; saved when the options menu is closed.
    /// </summary>
    public class SettingsService : MonoBehaviour {
        private static readonly string FileName = "settings.json";

        public GameSettings Current { get; private set; }

        private string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        private void Awake() {
            Current = Load();
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
        /// Applies current volume settings to the audio system.
        /// Currently stubbed — wire to AudioMixer when ready.
        /// </summary>
        public void ApplyVolume() {
            // TODO: Apply volume to AudioMixer or AudioListener.
            // Example: mixer.SetFloat("MusicVolume", ConvertToDecibels(Current.MusicVolume));
            Debug.Log($"Settings: Music={Current.MusicVolume}, SFX={Current.SfxVolume}");
        }
    }
}
