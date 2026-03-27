using Core.UI;
using Game.Core.Bootstrap;
using UnityEngine.SceneManagement;

namespace Game.UI.PauseMenu {
    public class PauseMenu : AnimatedWindow {
        public void OnExitToMenuClick() {
            G.Menu.CloseAll(() => {
                SceneManager.LoadScene("MainMenu");
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
