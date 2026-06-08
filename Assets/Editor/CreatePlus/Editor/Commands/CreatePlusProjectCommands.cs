using System;
using System.Collections.Generic;
using CreatePlus.Core;
using UnityEngine;

namespace CreatePlus.Commands {
    /// <summary>
    /// Provides the "Project" panel commands: project-specific create actions that appear in this
    /// project's Create menu before the standard Unity items. For the MVP these are registered as
    /// placeholders (they map to custom submenus, not single leaf actions). They stay visible and
    /// searchable, and their original menu path is recorded for future real execution and for the
    /// "Show Original Path" action.
    /// </summary>
    public sealed class CreatePlusProjectCommands : ICreatePlusCommandProvider {
        const string Panel = "Project";
        const string Source = "Project";

        public IEnumerable<CreatePlusCommand> GetCommands() {
            var list = new List<CreatePlusCommand>();

            const string game = "Game";
            list.Add(Placeholder("project.vengeful-spirit", "Vengeful Spirit", game, "Assets/Create/Vengeful Spirit", new[] { "vs" }));
            list.Add(Placeholder("project.config", "Config", game, "Assets/Create/Config", new[] { "settings", "so" }));
            list.Add(Placeholder("project.defs", "Defs", game, "Assets/Create/Defs", new[] { "definition", "so" }));

            const string content = "Content";
            list.Add(Placeholder("project.audio", "Audio", content, "Assets/Create/Audio", new[] { "sound", "sfx" }));
            list.Add(Placeholder("project.tiles", "Tiles", content, "Assets/Create/Tiles", new[] { "tile", "tilemap" }));
            list.Add(Placeholder("project.localization", "Localization", content, "Assets/Create/Localization", new[] { "loc", "i18n" }));

            const string pipeline = "Pipeline";
            list.Add(Placeholder("project.addressables", "Addressables", pipeline, "Assets/Create/Addressables", new[] { "addr" }));
            list.Add(Placeholder("project.tools", "Tools", pipeline, "Assets/Create/Tools", new[] { "util" }));

            return list;
        }

        static CreatePlusCommand Placeholder(string id, string name, string group, string path, string[] aliases) {
            return new CreatePlusCommand {
                Id = id,
                DisplayName = name,
                OriginalPath = path,
                GroupName = group,
                PanelName = Panel,
                Kind = CreatePlusCommandKind.ProjectCommand,
                Execute = context => Debug.Log("[Create Plus] Command is registered but execution is not implemented yet: " + path),
                Aliases = aliases ?? Array.Empty<string>(),
                Tooltip = name + " — original path: " + path,
                Source = Source,
                IsImplemented = false
            };
        }
    }
}
