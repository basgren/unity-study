using Core.Services;
using Core.UI;

namespace Game.UI.OptionsMenu {
    public class OptionsMenu : AnimatedWindow {
        public void OnBackClick() {
            G.Menu.CloseTopWindow();
        }
    }
}
