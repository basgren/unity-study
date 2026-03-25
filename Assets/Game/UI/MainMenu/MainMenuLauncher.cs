using System.Collections;
using Core.Services;
using UnityEngine;

namespace Game.UI {
    public enum MenuType {
        MainMenu,
        OptionsMenu,
        PauseMenu,
    }
    
    public class MainMenuLauncher : MonoBehaviour {
        [SerializeField]
        private MenuType startMenu = MenuType.MainMenu;
        
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
                    G.Menu.OpenMainMenu();
                    break;
            }
        }
    }
}
