using Core.Services;
using Core.UI;
using UnityEngine.SceneManagement;

namespace Game.UI.PauseMenu {
    public class PauseMenu : AnimatedWindow {
        public void OnExitToMenuClick() {
            G.Menu.CloseAll(() => {
                SceneManager.LoadScene("MainMenuScene");
            });
        }
        
        public void OnOptionsClick() {
            G.Menu.OpenOptionsMenu();
        }
        
        public void OnContinueClick() {
            G.Menu.CloseTopWindow();
        }
    }
}
