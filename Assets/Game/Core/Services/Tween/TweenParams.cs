using System;

namespace Game.Core.Services.Tween {
    /// <summary>
    /// Immutable parameters for a tween. Passed to <see cref="TweenService.StartTween"/>.
    /// </summary>
    public readonly struct TweenParams {
        public readonly float Duration;
        public readonly EaseType EaseType;
        public readonly bool UseUnscaledTime;
        public readonly Action OnBeforeStart;
        public readonly Action<float, float> OnUpdate;
        public readonly Action OnComplete;
        public readonly Action OnStop;

        public TweenParams(
            float duration,
            EaseType easeType,
            bool useUnscaledTime,
            Action onBeforeStart,
            Action<float, float> onUpdate,
            Action onComplete,
            Action onStop
        ) {
            Duration = duration;
            EaseType = easeType;
            UseUnscaledTime = useUnscaledTime;
            OnBeforeStart = onBeforeStart;
            OnUpdate = onUpdate;
            OnComplete = onComplete;
            OnStop = onStop;
        }
    }
}
