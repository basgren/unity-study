using Core.Services;
using Core.UI;
using UnityEngine.SceneManagement;

namespace Game.UI.PauseMenu {
    public class PauseMenu : AnimatedWindow {
        public void OnRestartClick() {
            G.Menu.CloseAll(() => {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            });
        }
        
        public void OnOptionsClick() {
            G.Menu.OpenOptionsMenu();
        }
        
        public void OnBackClick() {
            G.Menu.CloseTopWindow();
        }
    }
}
