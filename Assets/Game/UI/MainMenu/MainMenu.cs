using Core.Services;
using Core.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI.MainMenu {
    public class MainMenu : AnimatedWindow {
        private static readonly string StartScene = "IntroLevel";
        
        public void OnStartGameClick() {
            G.Menu.CloseAll(() => SceneManager.LoadScene(StartScene));
        }

        public void OnOptionsClick() {
            G.Menu.OpenOptionsMenu();
        }

        public void OnExitClick() {
            G.Menu.CloseAll();
            Application.Quit();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
