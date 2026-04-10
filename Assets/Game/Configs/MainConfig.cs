using Game.Audio.Dialog;
using Game.Core.Services.SceneState;
using Game.Core.UI;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;

namespace Game.Configs {
    /// <summary>
    /// Main configuration for the game. Contains references to other configs - scriptable object references.
    /// </summary>
    [CreateAssetMenu(menuName = "Config/Main Config", fileName = "MainConfig")]
    public class MainConfig : ScriptableObject {
        [Header("Player")]
        public PlayerConfig Player;

        [Header("Audio")]
        public AudioMixer AudioMixer;
        public AudioMixerGroup SfxMixerGroup;
        public DialogSoundLibrary DialogSoundLibrary;

        [Header("Menus")]
        public MenuWindow MainMenu;
        public MenuWindow OptionsMenu;
        public MenuWindow PauseMenu;

        [Header("In-Game UI")]
        public MenuWindow InventoryPanel;
        public MenuWindow DialogPanel;
        public MenuWindow ShopPanel;
        
        [Header("System")]
        public EventSystem eventSystem;
        
        [Header("Scene State")]
        public SceneCatalog SceneCatalog;

        [Header("Debug")]
        public bool EscQuitsImmediately;
    }
}
