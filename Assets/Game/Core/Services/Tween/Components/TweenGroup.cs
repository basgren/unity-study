using System;
using Game.Core.UI;
using UnityEngine;

namespace Game.Core.Services.Tween.Components {
    /// <summary>
    /// Collects child <see cref="TweenPreset"/> components and provides lookup by <see cref="TweenPreset.PresetId"/>.
    /// </summary>
    public class TweenGroup : MonoBehaviour {
        [SerializeField]
        private TweenPreset[] presets;

        private void Awake() {
            presets = GetComponentsInChildren<TweenPreset>();
        }

        /// <summary>
        /// Returns the first preset matching the given id, or null if not found.
        /// </summary>
        public TweenPreset Get(string id) {
            for (int i = 0; i < presets.Length; i++) {
                if (string.Equals(presets[i].PresetId, id, StringComparison.Ordinal)) {
                    return presets[i];
                }
            }

            Debug.LogWarning($"TweenGroup: preset '{id}' not found on '{name}'.");
            return null;
        }

        /// <summary>
        /// Plays the preset matching the given id. Returns the handle, or null if not found.
        /// </summary>
        public TweenHandle Play(string id, Action<float, float> onUpdate) {
            return Get(id)?.Play(onUpdate);
        }

        /// <summary>
        /// Plays the preset matching the given id with a completion callback.
        /// </summary>
        public TweenHandle Play(string id, Action<float, float> onUpdate, Action onComplete) {
            return Get(id)?.Play(onUpdate, onComplete);
        }

        /// <summary>
        /// Stops all presets in this group.
        /// </summary>
        public void StopAll() {
            for (int i = 0; i < presets.Length; i++) {
                presets[i].Stop();
            }
        }
    }
}