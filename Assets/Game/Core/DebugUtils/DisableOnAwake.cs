using UnityEngine;

namespace Game.Core.DebugUtils {
    /// <summary>
    /// Use this component for objects that should be visible in editor but disabled in runtime.
    /// Useful for UI components that are added to scene for editor purposes but should not be visible in runtime.
    /// </summary>
    public class DisableOnAwake : MonoBehaviour {
        private void Awake() {
            gameObject.SetActive(false);
        }
    }
}
