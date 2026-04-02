using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.Services.Tween {
    /// <summary>
    /// Lightweight tween service for UI and presentation effects.
    /// Owns the active tween list and drives updates; progression logic lives in <see cref="TweenInstance"/>.
    /// Exposed as <see cref="Bootstrap.G.Tween"/>.
    /// </summary>
    public sealed class TweenService : MonoBehaviour {
        private readonly List<TweenHandle> activeTweens = new();

        /// <summary>
        /// Starts a tween with the given parameters.
        /// Returns null if <paramref name="tweenParams"/> has Duration &lt; 0 or OnUpdate is null.
        /// </summary>
        public TweenHandle StartTween(TweenParams tweenParams) {
            if (tweenParams.Duration < 0f) {
                Debug.LogError("TweenService: Duration must be >= 0.");
                return null;
            }

            if (tweenParams.OnUpdate == null) {
                Debug.LogError("TweenService: OnUpdate callback is required.");
                return null;
            }

            var instance = new TweenInstance(tweenParams);
            var handle = new TweenHandle(instance);

            if (tweenParams.Duration <= 0f) {
                instance.Tick(0f);
                return handle;
            }

            activeTweens.Add(handle);
            return handle;
        }

        /// <summary>
        /// Creates a fluent builder for configuring a tween.
        /// </summary>
        public TweenBuilder New() {
            return new TweenBuilder(this);
        }

        /// <summary>
        /// Creates a fluent builder pre-filled with duration and update callback.
        /// </summary>
        public TweenBuilder New(float duration, Action<float, float> onUpdate) {
            return new TweenBuilder(this, duration, onUpdate);
        }

        private void Update() {
            for (int i = activeTweens.Count - 1; i >= 0; i--) {
                TweenHandle handle = activeTweens[i];
                TweenInstance instance = handle.Instance;

                if (instance.IsFinished) {
                    activeTweens.RemoveAt(i);
                    continue;
                }

                float dt = instance.UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                instance.Tick(dt);

                if (instance.IsFinished) {
                    activeTweens.RemoveAt(i);
                }
            }
        }
    }
}
