using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Widgets.ProgressBar {
    public class ProgressBar : MonoBehaviour {
        [SerializeField]
        private Image fillImage;

        public void SetProgress(float progress) {
            fillImage.fillAmount = progress;
        } 
    }
}
