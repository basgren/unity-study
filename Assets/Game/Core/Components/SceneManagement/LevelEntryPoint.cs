using Core.Audio;
using Game.Core.Bootstrap;
using UnityEngine;

namespace Game.Core.Components.SceneManagement {
    /// <summary>
    /// Per-scene hook that tells the central audio service which music cue this scene wants.
    /// Music ownership (play/stop/fade) lives on <c>G.Audio</c>, which persists across scene
    /// loads — so this component intentionally does NOT stop music on teardown. The next
    /// scene's LevelEntryPoint decides whether to keep the same track or fade to a new one.
    /// Assign a null cue to request silence for this scene.
    /// </summary>
    public class LevelEntryPoint : MonoBehaviour {
        [SerializeField]
        private AudioCue levelMusic;

        private void Start() {
            G.Audio.SetLevelMusic(levelMusic);
        }
    }
}
