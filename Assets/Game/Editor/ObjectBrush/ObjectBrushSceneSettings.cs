using System;
using System.Collections.Generic;
using UnityEngine;

namespace Editor.ObjectBrush {
    [Serializable]
    public class CategoryParentBinding {
        public string categoryName;
        public string parentHierarchyPath; // "Root/Props/Barrels"
    }

    [Serializable]
    public class SceneParentSettings {
        public string scenePath; // "Assets/Scenes/Level1.unity"
        public List<CategoryParentBinding> categoryBindings = new List<CategoryParentBinding>();
    }

    /// <summary>
    /// Scene-specific parent bindings for ObjectBrush categories.
    /// Stored as a project asset so it can be version-controlled (Git).
    /// </summary>
    public class ObjectBrushSceneSettings : ScriptableObject {
        public List<SceneParentSettings> scenes = new List<SceneParentSettings>();

        public const string DefaultAssetPath = "Assets/Editor/ObjectBrushSceneSettings.asset";
    }
}
