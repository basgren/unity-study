using UnityEngine;

namespace Core.Audio {
    /// <summary>
    /// Add this component to an object with AudioListener (not camera!), and specify followTarget, so each
    /// frame this object will update its position to target position, but with Z = 0. As the whole world
    /// is places at z = 0, this will allow to make coorect calculations of distance from audio source to
    /// audio listeners. Adding audio listener to camera may not work, as camera usually has Z &lt; 0
    /// (often Z = -10). While binding audio listener to player may not give desired effect, especially if
    /// camera doesn't follow player all the time (in cutscenes, for example). 
    /// </summary>
    public class AudioListenerFollow2D : MonoBehaviour {
        [SerializeField]
        private Transform followTarget;
        
        private void Awake() {
            if (followTarget == null && Camera.main != null) {
                followTarget = Camera.main.transform;
            }
        }
        
        private void LateUpdate() {
            if (followTarget == null) {
                return;
            }

            var p = followTarget.position;
            transform.position = new Vector3(p.x, p.y, 0f);
        }
    }
}
