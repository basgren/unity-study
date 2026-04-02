using System;

namespace Game.Core.Services.Tween {
    /// <summary>
    /// Fluent builder for constructing and starting tweens.
    /// Created via <see cref="TweenService.New()"/>.
    /// </summary>
    public sealed class TweenBuilder {
        private readonly TweenService service;
        private float duration;
        private EaseType easeType = EaseType.Linear;
        private bool useUnscaledTime;
        private Action onBeforeStart;
        private Action<float, float> onUpdate;
        private Action onComplete;
        private Action onStop;

        internal TweenBuilder(TweenService service) {
            this.service = service;
        }

        internal TweenBuilder(TweenService service, float duration, Action<float, float> onUpdate) {
            this.service = service;
            this.duration = duration;
            this.onUpdate = onUpdate;
        }

        public TweenBuilder Duration(float value) {
            duration = value;
            return this;
        }

        public TweenBuilder Ease(EaseType value) {
            easeType = value;
            return this;
        }

        public TweenBuilder UnscaledTime(bool value) {
            useUnscaledTime = value;
            return this;
        }

        public TweenBuilder OnBeforeStart(Action callback) {
            onBeforeStart = callback;
            return this;
        }

        public TweenBuilder OnUpdate(Action<float, float> callback) {
            onUpdate = callback;
            return this;
        }

        public TweenBuilder OnComplete(Action callback) {
            onComplete = callback;
            return this;
        }

        public TweenBuilder OnStop(Action callback) {
            onStop = callback;
            return this;
        }

        /// <summary>
        /// Builds <see cref="TweenParams"/> and starts the tween.
        /// Returns null if validation fails (Duration &lt; 0 or OnUpdate is null).
        /// </summary>
        public TweenHandle Start() {
            var tweenParams = new TweenParams(
                duration: duration,
                easeType: easeType,
                useUnscaledTime: useUnscaledTime,
                onBeforeStart: onBeforeStart,
                onUpdate: onUpdate,
                onComplete: onComplete,
                onStop: onStop
            );
            return service.StartTween(tweenParams);
        }
    }
}
