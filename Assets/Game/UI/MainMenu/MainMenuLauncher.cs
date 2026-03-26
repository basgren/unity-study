using System.Collections;
using Game.Core.Services.Bootstrap;
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
