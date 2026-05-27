using System.Collections.Generic;
using UnityEngine;

namespace Editor.ObjectBrush {
    /// <summary>
    /// Shared, project-wide configuration for the Object Brush.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stored as a single project asset (see <see cref="DefaultAssetPath"/>) so it is
    /// version-controlled and identical across every scene. It holds the World root name
    /// used by the name-based parenting convention and the list of biome profiles that are
    /// referenced (and shown) simultaneously by the Object Brush.
    /// </para>
    /// <para>
    /// Parenting convention: placed objects are nested under
    /// <c>&lt;worldRootName&gt;/&lt;category parent path&gt;</c> in the active scene, creating
    /// any missing objects along the way. This replaces the previous per-scene parent bindings.
    /// </para>
    /// </remarks>
    public class ObjectBrushConfig : ScriptableObject {
        [Tooltip("Name of the root object in each scene under which placed objects are nested. " +
                 "Leave empty to place directly at scene root.")]
        public string worldRootName = "World";

        [Tooltip("Biome profiles referenced by the Object Brush. All are shown at once.")]
        public List<ObjectBrushProfile> biomes = new List<ObjectBrushProfile>();

        public const string DefaultAssetPath = "Assets/Editor/ObjectBrushConfig.asset";
    }
}
