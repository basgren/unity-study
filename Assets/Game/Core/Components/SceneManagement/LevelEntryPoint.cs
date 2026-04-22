using Core.Audio;
using Game.Core.Bootstrap;
using UnityEngine;

namespace Game.Core.Components.SceneManagement {
    /// <summary>
    /// Per-scene hook that registers the level's music cue with the audio service.
    /// Music ownership (play/stop/duck) lives on <c>G.Audio</c>; this component just
    /// tells the service which cue to use for the current level.
    /// </summary>
    public class LevelEntryPoint : MonoBehaviour {
        [SerializeField]
        private AudioCue levelMusic;

        private void Start() {
            if (levelMusic != null) {
                G.Audio.SetLevelMusic(levelMusic);
            }
        }

        private void OnDestroy() {
            if (G.Audio != null) {
                G.Audio.ClearLevelMusic();
            }
        }
    }
}
