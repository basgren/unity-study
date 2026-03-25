using UnityEngine;

namespace Core.Audio {
    public interface IAudioLoopHandle {
        bool IsValid { get; }

        void Stop(float fadeOutSeconds = 0.05f);

        void SetVolume(float volume);

        void SetPitch(float pitch);

        void SetPosition(Vector3 worldPosition);
    }
}
