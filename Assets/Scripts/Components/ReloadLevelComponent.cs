using UnityEngine;
using UnityEngine.SceneManagement;

namespace Components {
    public class ReloadLevelComponent : MonoBehaviour {
        // TODO: consider using some global service for level reload. It's not optimal that we have to
        //   add this component to any object that requires level reload.  
        public void ReloadLevel() {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
