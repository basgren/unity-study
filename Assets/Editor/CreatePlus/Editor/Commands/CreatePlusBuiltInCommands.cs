using System;
using System.Collections.Generic;
using CreatePlus.Core;
using UnityEngine;

namespace CreatePlus.Commands {
    /// <summary>
    /// Provides the "Unity Common" panel commands: common, useful create actions. A handful are fully
    /// implemented (folder, script, material, scene, text, assembly definitions); the rest are
    /// registered as placeholders so they are visible and searchable but log a clear message until
    /// real execution is wired up. Commands are never silently dropped.
    /// </summary>
    public sealed class CreatePlusBuiltInCommands : ICreatePlusCommandProvider {
        const string Panel = "Unity Common";
        const string Source = "Unity";

        public IEnumerable<CreatePlusCommand> GetCommands() {
            var list = new List<CreatePlusCommand>();

            // ---- Core --------------------------------------------------------------------------
            const string core = "Core";
            list.Add(Implemented("builtin.asset.folder", "Folder", core, "Assets/Create/Folder",
                CreatePlusAssetFactory.CreateFolder, new[] { "dir", "directory" }));
            list.Add(Implemented("builtin.asset.csharp-script", "C# Script", core, "Assets/Create/C# Script",
                CreatePlusAssetFactory.CreateCSharpScript, new[] { "cs", "code", "monobehaviour" }));
            list.Add(Implemented("builtin.asset.scene", "Scene", core, "Assets/Create/Scene",
                CreatePlusAssetFactory.CreateScene, new[] { "level" }));
            list.Add(Implemented("builtin.asset.text", "Text", core, "Assets/Create/Text",
                CreatePlusAssetFactory.CreateTextFile, new[] { "txt", "note" }));
            list.Add(Implemented("builtin.asset.asmdef", "Assembly Definition", core, "Assets/Create/Assembly Definition",
                CreatePlusAssetFactory.CreateAssemblyDefinition, new[] { "asmdef", "asmref", "assembly" }));
            list.Add(Implemented("builtin.asset.asmref", "Assembly Definition Reference", core, "Assets/Create/Assembly Definition Reference",
                CreatePlusAssetFactory.CreateAssemblyDefinitionReference, new[] { "asmref", "assembly" }));

            // ---- 2D / Level --------------------------------------------------------------------
            const string twoD = "2D / Level";
            list.Add(Placeholder("builtin.2d", "2D", twoD, "Assets/Create/2D", new[] { "sprite", "tile" }));
            list.Add(Placeholder("builtin.tiles", "Tiles", twoD, "Assets/Create/2D/Tiles", new[] { "tile", "tilemap" }));
            list.Add(Placeholder("builtin.asset.physics-material-2d", "Physics Material 2D", twoD, "Assets/Create/2D/Physics Material 2D", new[] { "physics", "friction" }));

            // ---- Graphics / Rendering ----------------------------------------------------------
            const string gfx = "Graphics / Rendering";
            list.Add(Implemented("builtin.asset.material", "Material", gfx, "Assets/Create/Material",
                CreatePlusAssetFactory.CreateMaterial, new[] { "mat", "shader" }));
            list.Add(Placeholder("builtin.asset.material-variant", "Material Variant", gfx, "Assets/Create/Material Variant", new[] { "mat" }));
            list.Add(Placeholder("builtin.shadergraph", "Shader Graph", gfx, "Assets/Create/Shader Graph", new[] { "shader", "graph" }));
            list.Add(Placeholder("builtin.rendering", "Rendering", gfx, "Assets/Create/Rendering", new[] { "render", "pipeline" }));
            list.Add(Placeholder("builtin.asset.render-texture", "Render Texture", gfx, "Assets/Create/Render Texture", new[] { "rt", "texture" }));
            list.Add(Placeholder("builtin.asset.custom-render-texture", "Custom Render Texture", gfx, "Assets/Create/Custom Render Texture", new[] { "crt", "texture" }));
            list.Add(Placeholder("builtin.shader", "Shader", gfx, "Assets/Create/Shader", new[] { "shader" }));
            list.Add(Placeholder("builtin.asset.shader-variant-collection", "Shader Variant Collection", gfx, "Assets/Create/Shader Variant Collection", new[] { "shader", "variant" }));
            list.Add(Placeholder("builtin.lens-flare", "Lens Flare", gfx, "Assets/Create/Lens Flare", new[] { "flare" }));
            list.Add(Placeholder("builtin.lens-flare-srp", "Lens Flare (SRP)", gfx, "Assets/Create/Lens Flare (SRP)", new[] { "flare", "srp" }));
            list.Add(Placeholder("builtin.lighting-settings", "Lighting Settings", gfx, "Assets/Create/Lighting Settings", new[] { "light" }));
            list.Add(Placeholder("builtin.lightmap-parameters", "Lightmap Parameters", gfx, "Assets/Create/Lightmap Parameters", new[] { "light", "lightmap" }));
            list.Add(Placeholder("builtin.volume-profile", "Volume Profile", gfx, "Assets/Create/Volume Profile", new[] { "post", "volume" }));

            // ---- Animation / Audio / Timeline --------------------------------------------------
            const string anim = "Animation / Audio / Timeline";
            list.Add(Placeholder("builtin.animator-controller", "Animator Controller", anim, "Assets/Create/Animator Controller", new[] { "anim", "animation" }));
            list.Add(Placeholder("builtin.animation", "Animation", anim, "Assets/Create/Animation", new[] { "anim", "clip" }));
            list.Add(Placeholder("builtin.animator-override-controller", "Animator Override Controller", anim, "Assets/Create/Animator Override Controller", new[] { "anim", "override" }));
            list.Add(Placeholder("builtin.audio-mixer", "Audio Mixer", anim, "Assets/Create/Audio Mixer", new[] { "audio", "sound", "mixer" }));
            list.Add(Placeholder("builtin.avatar-mask", "Avatar Mask", anim, "Assets/Create/Avatar Mask", new[] { "anim", "mask" }));
            list.Add(Placeholder("builtin.timeline", "Timeline", anim, "Assets/Create/Timeline", new[] { "cutscene" }));
            list.Add(Placeholder("builtin.signal", "Signal", anim, "Assets/Create/Signal", new[] { "timeline" }));

            // ---- UI / Text ---------------------------------------------------------------------
            const string ui = "UI / Text";
            list.Add(Placeholder("builtin.ui-toolkit", "UI Toolkit", ui, "Assets/Create/UI Toolkit", new[] { "uxml", "uss", "ui" }));
            // TextMeshPro is a submenu, not a single command: it renders as a nested "TextMeshPro"
            // subgroup inside "UI / Text", mirroring Unity's familiar Create hierarchy.
            string[] tmp = { "TextMeshPro" };
            list.Add(Placeholder("builtin.tmp.font-asset", "Font Asset", ui, "Assets/Create/TextMeshPro/Font Asset", new[] { "tmp", "text", "font" }, tmp));
            list.Add(Placeholder("builtin.tmp.sprite-asset", "Sprite Asset", ui, "Assets/Create/TextMeshPro/Sprite Asset", new[] { "tmp", "sprite" }, tmp));
            list.Add(Placeholder("builtin.tmp.color-gradient", "Color Gradient", ui, "Assets/Create/TextMeshPro/Color Gradient", new[] { "tmp", "gradient", "color" }, tmp));
            list.Add(Placeholder("builtin.tmp.style-sheet", "Style Sheet", ui, "Assets/Create/TextMeshPro/Style Sheet", new[] { "tmp", "style" }, tmp));
            list.Add(Placeholder("builtin.gui-skin", "GUI Skin", ui, "Assets/Create/GUI Skin", new[] { "imgui", "skin" }));
            list.Add(Placeholder("builtin.custom-font", "Custom Font", ui, "Assets/Create/Custom Font", new[] { "font", "text" }));
            list.Add(Placeholder("builtin.legacy", "Legacy", ui, "Assets/Create/Legacy", new[] { "old" }));

            // ---- Packages / Tools --------------------------------------------------------------
            const string pkg = "Packages / Tools";
            list.Add(Placeholder("package.cinemachine", "Cinemachine", pkg, "Assets/Create/Cinemachine", new[] { "camera", "cm" }));
            list.Add(Placeholder("package.input-actions", "Input Actions", pkg, "Assets/Create/Input Actions", new[] { "input", "controls" }));
            list.Add(Placeholder("package.testing", "Testing", pkg, "Assets/Create/Testing", new[] { "test", "nunit" }));
            list.Add(Placeholder("package.search", "Search", pkg, "Assets/Create/Search", new[] { "query" }));
            list.Add(Placeholder("package.addressables", "Addressables", pkg, "Assets/Create/Addressables", new[] { "addr", "asset" }));
            list.Add(Placeholder("package.localization", "Localization", pkg, "Assets/Create/Localization", new[] { "loc", "i18n", "translation" }));

            // ---- Advanced / Rare ---------------------------------------------------------------
            const string rare = "Advanced / Rare";
            list.Add(Placeholder("builtin.scene-template", "Scene Template", rare, "Assets/Create/Scene Template", new[] { "scene" }));
            list.Add(Placeholder("builtin.scene-template-from-scene", "Scene Template From Scene", rare, "Assets/Create/Scene Template From Scene", new[] { "scene" }));
            list.Add(Placeholder("builtin.scene-template-pipeline", "Scene Template Pipeline", rare, "Assets/Create/Scene Template Pipeline", new[] { "scene" }));
            list.Add(Placeholder("builtin.prefab-variant", "Prefab Variant", rare, "Assets/Create/Prefab Variant", new[] { "prefab" }));
            list.Add(Placeholder("builtin.asset.physic-material", "Physic Material", rare, "Assets/Create/Physic Material", new[] { "physics", "3d" }));

            return list;
        }

        static CreatePlusCommand Implemented(string id, string name, string group, string path,
                                             Action<CreatePlusContext> execute, string[] aliases) {
            return new CreatePlusCommand {
                Id = id,
                DisplayName = name,
                OriginalPath = path,
                GroupName = group,
                PanelName = Panel,
                Kind = CreatePlusCommandKind.BuiltInAssetCommand,
                Execute = execute,
                Aliases = aliases ?? Array.Empty<string>(),
                Tooltip = "Create " + name + " in the selected folder.",
                Source = Source,
                IsImplemented = true
            };
        }

        static CreatePlusCommand Placeholder(string id, string name, string group, string path, string[] aliases,
                                             string[] subGroupPath = null) {
            return new CreatePlusCommand {
                Id = id,
                DisplayName = name,
                OriginalPath = path,
                GroupName = group,
                SubGroupPath = subGroupPath ?? Array.Empty<string>(),
                PanelName = Panel,
                Kind = CreatePlusCommandKind.BuiltInAssetCommand,
                Execute = context => LogNotImplemented(path),
                Aliases = aliases ?? Array.Empty<string>(),
                Tooltip = name + " — original path: " + path,
                Source = Source,
                IsImplemented = false
            };
        }

        static void LogNotImplemented(string originalPath) {
            Debug.Log("[Create Plus] Command is registered but execution is not implemented yet: " + originalPath);
        }
    }
}
