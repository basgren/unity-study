using Core.UI;
using UnityEngine;
using UnityEngine.Audio;

namespace Game.Configs {
    /// <summary>
    /// Main configuration for the game. Contains references to other configs - scriptable object references.
    /// </summary>
    [CreateAssetMenu(menuName = "Config/Main Config", fileName = "MainConfig")]
    public class MainConfig : ScriptableObject {
        public PlayerConfig Player;

        [Header("Audio")]
        public AudioMixer AudioMixer;
        public AudioMixerGroup SfxMixerGroup;

        [Header("Menus")]
        public AnimatedWindow MainMenu;
        public AnimatedWindow OptionsMenu;
        public AnimatedWindow PauseMenu;

        [Header("Debug")]
        public bool EscQuitsImmediately;
    }
}
