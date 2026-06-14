using Game.Core.Bootstrap;
using Game.Core.Services.Scene;
using Game.Core.UI;

namespace Game.UI.PauseMenu {
    public class PauseMenu : AnimatedWindow {
        public void OnExitToMenuClick() {
            // SkipStateCapture: the main menu has no gameplay state to preserve.
            SceneTransition.FadeToScene("MainMenu", new SceneLoadOptions { SkipStateCapture = true });
        }

        public void OnOptionsClick() {
            G.Menu.OpenOptionsMenu();
        }

        public void OnContinueClick() {
            G.Menu.CloseTopWindow();
        }
    }
}
