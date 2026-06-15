#if UNITY_EDITOR
namespace Game.Editor.SceneTools {
    /// <summary>Where the window's selectable scene list comes from.</summary>
    public enum SceneSource {
        /// <summary>Scenes listed in Build Settings.</summary>
        BuildSettings,

        /// <summary>Every scene asset under the project's Assets folder.</summary>
        AllProject,
    }
}
#endif
