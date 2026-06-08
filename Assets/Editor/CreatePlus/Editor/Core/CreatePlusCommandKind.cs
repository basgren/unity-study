namespace CreatePlus.Core {
    /// <summary>
    /// Classifies the origin and behavior of a Create Plus command.
    /// Used for grouping, filtering, and future automatic discovery.
    /// </summary>
    public enum CreatePlusCommandKind {
        /// <summary>Built-in Unity command that creates a project asset (folder, material, script, ...).</summary>
        BuiltInAssetCommand,

        /// <summary>Built-in Unity command that creates or affects a scene object.</summary>
        BuiltInSceneCommand,

        /// <summary>Project-specific create command (custom MenuItem, ScriptableObject factory, ...).</summary>
        ProjectCommand,

        /// <summary>Shortcut that instantiates a specific prefab.</summary>
        PrefabShortcut,

        /// <summary>User- or package-registered factory command.</summary>
        CustomFactory,

        /// <summary>Command that could not be classified.</summary>
        Unknown
    }
}
