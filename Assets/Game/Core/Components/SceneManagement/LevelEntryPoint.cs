using Core.Audio;
using Game.Core.Bootstrap;
using UnityEngine;

namespace Game.Core.Components.SceneManagement {
    public class LevelEntryPoint : MonoBehaviour {
        [SerializeField]
        private AudioCue levelMusic;

        private IAudioLoopHandle audioHandle;
        
        void Start() {
            if (levelMusic != null) {
                audioHandle = G.Audio.PlayLoopAt(levelMusic, transform.position, false);
            }
        }
        
        private void OnDestroy() {
            if (audioHandle != null) {
                audioHandle.Stop();
                audioHandle = null;
            }
        }
    }
}
