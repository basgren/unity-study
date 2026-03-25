using System;
using System.Collections.Generic;
using UnityEngine;

namespace Editor.ObjectBrush {
    /// <summary>
    /// ScriptableObject profile that stores reusable palette data for the Object Brush.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A biome profile is an asset that contains a set of categories and prefab references
    /// for the <see cref="ObjectBrushWindow"/>. It is intended to represent a "biome"
    /// or level theme (for example: Beach, Ship, Island).
    /// </para>
    ///
    /// <para><b>What is stored</b></para>
    /// <list type="bullet">
    ///   <item>
    ///     <description>Category names.</description>
    ///   </item>
    ///   <item>
    ///     <description>Prefab references assigned to each category.</description>
    ///   </item>
    /// </list>
    ///
    /// <para><b>What is NOT stored</b></para>
    /// <list type="bullet">
    ///   <item>
    ///     <description>Scene-specific parent transforms.</description>
    ///   </item>
    ///   <item>
    ///     <description>Brush state (enabled/disabled, active prefab, filter text, grid settings).</description>
    ///   </item>
    /// </list>
    ///
    /// <para><b>Typical workflow</b></para>
    /// <list type="number">
    ///   <item>
    ///     <description>
    ///       Create a profile asset via <c>Create &gt; Tools &gt; Object Brush Biome Profile</c>
    ///       and name it after the biome or level theme (for example: <c>Biome_Island</c>).
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       In <see cref="ObjectBrushWindow"/>, configure categories and palette items
    ///       (prefabs) for this biome.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       Assign the profile to the <c>Profile</c> field and press <c>Save</c>
    ///       to write the current palette into the asset.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       In another scene or project setup, assign the same profile and press <c>Load</c>
    ///       to restore the same categories and prefabs, then set scene-specific parents
    ///       if needed.
    ///     </description>
    ///   </item>
    /// </list>
    /// </remarks>
    [CreateAssetMenu(
        fileName = "NewObjectBrushBiome",
        menuName = "Tools/Object Brush Biome Profile")]
    public class ObjectBrushProfile : ScriptableObject {
        [Serializable]
        public class BiomeCategory {
            public string name;
            public List<GameObject> items = new List<GameObject>();
        }

        public List<BiomeCategory> categories = new List<BiomeCategory>();
    }
}
