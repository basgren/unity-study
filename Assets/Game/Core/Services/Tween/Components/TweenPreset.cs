using System;
using Game.Core.Bootstrap;
using UnityEngine;

namespace Game.Core.Services.Tween.Components {
    /// <summary>
    /// Inspector-driven tween preset. Designers set duration/easing once;
    /// game code only calls Play/Pause/Resume/Stop.
    /// </summary>
    public class TweenPreset : MonoBehaviour {
        /// <summary>
        /// Optional name to be used with TweenGroup 
        /// </summary>
        [SerializeField]
        private string presetId;
        
        [SerializeField]
        private float duration = 0.25f;
        
        [SerializeField]
        private EaseType easeType = EaseType.OutCubic;
        
        [SerializeField]
        private bool useUnscaledTime = true;

        public string PresetId => presetId;
        
        private TweenHandle activeHandle;

        public TweenHandle Play(Action<float, float> onUpdate) {
            return Play(onUpdate, null);
        }

        public TweenHandle Play(Action<float, float> onUpdate, Action onComplete) {
            Stop();
            activeHandle = G.Tween.StartTween(new TweenParams(
                duration: duration,
                easeType: easeType,
                useUnscaledTime: useUnscaledTime,
                onBeforeStart: null,
                onUpdate: onUpdate,
                onComplete: onComplete,
                onStop: null
            ));
            return activeHandle;
        }

        public void Pause() {
            activeHandle?.Pause();
        }

        public void Resume() {
            activeHandle?.Resume();
        }

        public void Stop() {
            if (activeHandle != null && activeHandle.IsRunning) {
                activeHandle.Stop();
            }

            activeHandle = null;
        }
    }
}
