using Game.Audio.Dialog;
using Game.Core.Services.SceneState;
using Game.Core.UI;
using Game.Features.Effects.DeathScreen;
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

        [Header("Music")]
        [Tooltip("Seconds to fade the current track out before a different track starts.")]
        [Min(0f)]
        public float musicFadeOutTime = 0.5f;

        [Tooltip("Seconds for a newly started track to fade in to its target volume.")]
        [Min(0f)]
        public float musicFadeInTime = 0.5f;

        [Header("Menus")]
        public MenuWindow MainMenu;
        public MenuWindow OptionsMenu;
        public MenuWindow PauseMenu;
        public MenuWindow ConfirmDialog;

        [Header("In-Game UI")]
        public MenuWindow InventoryPanel;
        public MenuWindow DialogPanel;
        public MenuWindow ShopPanel;
        public MenuWindow StatShopPanel;
        
        [Header("System")]
        public EventSystem eventSystem;
        
        [Header("Scene State")]
        public SceneCatalog SceneCatalog;

        [Header("Scene Transitions")]
        [Tooltip("Seconds to fade to black before a menu-driven scene change (Continue, New Game, Exit to Menu).")]
        [Min(0f)]
        public float sceneFadeOutTime = 0.5f;

        [Tooltip("Seconds to fade back in after the new scene has loaded.")]
        [Min(0f)]
        public float sceneFadeInTime = 0.5f;

        [Header("Effects")]
        public DeathScreenSettings DeathScreen = new DeathScreenSettings();

        [Header("Debug")]
        public bool EscQuitsImmediately;

        [Header("Debug Systems")]
        public DebugSystemsConfig DebugSystems = new();
    }

    /// <summary>
    /// Toggles for optional debug-only systems loaded at startup. Each enabled system is announced
    /// with a runtime warning so it is not accidentally left on in a build. Add new debug toggles here.
    /// </summary>
    [System.Serializable]
    public class DebugSystemsConfig {
        public bool EnableDebugInventory;
    }
}
