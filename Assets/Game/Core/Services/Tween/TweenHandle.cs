namespace Game.Core.Services.Tween {
    /// <summary>
    /// Public control object for an active tween. Returned by <see cref="TweenService.StartTween"/>.
    /// </summary>
    public sealed class TweenHandle {
        internal readonly TweenInstance Instance;

        public bool IsRunning => Instance.IsRunning;
        public bool IsPaused => Instance.IsPaused;

        internal TweenHandle(TweenInstance instance) {
            Instance = instance;
        }

        public void Pause() {
            Instance.Pause();
        }

        public void Resume() {
            Instance.Resume();
        }

        public void Stop() {
            Instance.Stop();
        }
    }
}
