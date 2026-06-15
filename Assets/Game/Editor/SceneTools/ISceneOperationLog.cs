#if UNITY_EDITOR
using UnityEngine;

namespace Game.Editor.SceneTools {
    /// <summary>
    /// Sink for operation messages. The runner's implementation forwards to the Unity Console and
    /// prefixes each line with the current scene name.
    /// </summary>
    public interface ISceneOperationLog {
        void Info(string message, Object context = null);
        void Error(string message, Object context = null);
    }
}
#endif
