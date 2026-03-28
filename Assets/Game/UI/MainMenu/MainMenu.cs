using Game.Core.Bootstrap;
using Game.Core.UI;
using Game.Doors;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI.MainMenu {
    public class MainMenu : AnimatedWindow {
        [SerializeField]
        private SceneReference startScene;
        
        public void OnStartGameClick() {
            G.Menu.CloseAll(() => SceneManager.LoadScene(startScene.GetSceneName()));
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
