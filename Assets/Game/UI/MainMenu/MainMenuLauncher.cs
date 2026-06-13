using System.Collections;
using Core.Audio;
using Game.Core.Bootstrap;
using UnityEngine;

namespace Game.UI.MainMenu {
    public enum MenuType {
        MainMenu,
        OptionsMenu,
        PauseMenu,
    }
    
    public class MainMenuLauncher : MonoBehaviour {
        [SerializeField]
        private MenuType startMenu = MenuType.MainMenu;
        
        // Just to synchronize main menu with music
        [SerializeField]
        private float mainMenuDelay = 0.4f;
        
        [SerializeField]
        private AudioCue mainMenuMusic;
        
        private void Awake() {
            // Route through the central music service so menu music shares the single
            // persistent handle (prevents gameplay music playing over the menu).
            G.Audio.SetLevelMusic(mainMenuMusic);
        }
        
        private IEnumerator Start() {
            yield return null;
            yield return new WaitForEndOfFrame();

            switch (startMenu) {
                case MenuType.OptionsMenu:
                    G.Menu.OpenOptionsMenu();
                    break;
                
                case MenuType.PauseMenu:
                    G.Menu.OpenPauseMenu();
                    break;
                
                default:
                    StartCoroutine(ShowMainMenu(mainMenuDelay));
                    break;
            }
        }

        private IEnumerator ShowMainMenu(float delay) {
            yield return new WaitForSeconds(delay);
            G.Menu.OpenMainMenu();
        }
    }
}
