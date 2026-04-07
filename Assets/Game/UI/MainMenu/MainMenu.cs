using Game.Core.Bootstrap;
using Game.Core.Services.Scene;
using Game.Core.UI;
using UnityEngine;

namespace Game.UI.MainMenu {
    public class MainMenu : AnimatedWindow {
        [SerializeField]
        private SceneReference startScene;
        
        public void OnStartGameClick() {
            G.Menu.CloseAll(() => G.SceneTravel.LoadScene(startScene.GetSceneName()));
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
