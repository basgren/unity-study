using Game.Core.Bootstrap;
using UnityEngine;

namespace Core.Components.SceneManagement {
    public class ReloadLevelComponent : MonoBehaviour {
        public void ReloadLevel() {
            G.SceneTravel.ReloadActiveScene();
        }
    }
}
