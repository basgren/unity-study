using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]

namespace Game.Core.Services.Tween {
    /// <summary>
    /// Holds tween runtime state and owns all progression logic.
    /// Created and driven by <see cref="TweenService"/>, controlled via <see cref="TweenHandle"/>.
    /// </summary>
    internal sealed class TweenInstance {
        private readonly TweenParams tweenParams;
        private float elapsed;
        private bool started;
        private bool completed;
        private bool stopped;
        private bool paused;

        public bool IsRunning => started && !completed && !stopped;
        public bool IsPaused => paused;
        public bool IsFinished => completed || stopped;
        internal bool UseUnscaledTime => tweenParams.UseUnscaledTime;

        public TweenInstance(TweenParams tweenParams) {
            this.tweenParams = tweenParams;
        }

        public void Tick(float deltaTime) {
            if (IsFinished || paused) {
                return;
            }

            if (!started) {
                started = true;
                tweenParams.OnBeforeStart?.Invoke();

                if (tweenParams.Duration <= 0f) {
                    tweenParams.OnUpdate?.Invoke(1f, 1f);
                    MarkCompleted();
                    return;
                }
            }

            elapsed += deltaTime;
            float progress = Mathf.Clamp01(elapsed / tweenParams.Duration);
            float eased = Ease.Evaluate(tweenParams.EaseType, progress);
            tweenParams.OnUpdate?.Invoke(eased, progress);

            if (progress >= 1f) {
                MarkCompleted();
            }
        }

        public void Pause() {
            if (IsRunning) {
                paused = true;
            }
        }

        public void Resume() {
            if (paused && !IsFinished) {
                paused = false;
            }
        }

        public void Stop() {
            if (IsFinished) {
                return;
            }

            stopped = true;
            tweenParams.OnStop?.Invoke();
        }

        private void MarkCompleted() {
            completed = true;
            tweenParams.OnComplete?.Invoke();
        }
    }
}
