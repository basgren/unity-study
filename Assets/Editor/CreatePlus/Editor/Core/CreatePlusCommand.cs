using System;
using UnityEngine;

namespace CreatePlus.Core {
    /// <summary>
    /// UI-independent definition of a single create command.
    ///
    /// This is the immutable description of a command (identity, where it belongs, how to run it).
    /// Per-user mutable state (favorite, pinned, hidden, usage, last used) is NOT stored here; it
    /// lives in <see cref="CreatePlusSettings"/> keyed by <see cref="Id"/> so the registry can be
    /// rebuilt freely without losing user data. Use <see cref="CreatePlusSettingsStore"/> to query
    /// that state.
    /// </summary>
    public sealed class CreatePlusCommand {
        /// <summary>Stable, unique identifier (e.g. "builtin.asset.folder"). Never the display name.</summary>
        public string Id { get; set; }

        /// <summary>Human readable name shown in the palette.</summary>
        public string DisplayName { get; set; }

        /// <summary>Original Unity (or project) Create menu path, used for search and "Show Original Path".</summary>
        public string OriginalPath { get; set; }

        /// <summary>Group inside the panel (e.g. "Core", "Graphics / Rendering", "Game").</summary>
        public string GroupName { get; set; }

        /// <summary>
        /// Familiar Unity submenu path of this command inside its <see cref="GroupName"/>, mirroring
        /// the native Create menu hierarchy. Empty means the command is a direct leaf of the group.
        /// Example: a TextMeshPro "Font Asset" command in the "UI / Text" group uses ["TextMeshPro"],
        /// so it renders under a collapsible "TextMeshPro" subgroup. Supports arbitrary depth.
        /// </summary>
        public string[] SubGroupPath { get; set; } = Array.Empty<string>();

        /// <summary>Owning panel (e.g. "Project", "Unity Common").</summary>
        public string PanelName { get; set; }

        /// <summary>Optional icon shown to the left of the row. May be null.</summary>
        public Texture Icon { get; set; }

        /// <summary>Classification of this command.</summary>
        public CreatePlusCommandKind Kind { get; set; } = CreatePlusCommandKind.Unknown;

        /// <summary>
        /// Action that performs the command. Receives the execution context. Must not assume any
        /// particular UI is present. May be null for purely informational entries.
        /// </summary>
        public Action<CreatePlusContext> Execute { get; set; }

        /// <summary>Extra search keywords (e.g. "mat", "physic").</summary>
        public string[] Aliases { get; set; } = Array.Empty<string>();

        /// <summary>Tooltip describing the command.</summary>
        public string Tooltip { get; set; }

        /// <summary>Where the command came from (e.g. "Unity", "Project", package name).</summary>
        public string Source { get; set; }

        /// <summary>
        /// True when <see cref="Execute"/> actually creates something. When false the command is a
        /// registered placeholder whose execution only logs a message (the palette stays open and
        /// does not record usage).
        /// </summary>
        public bool IsImplemented { get; set; }

        /// <summary>
        /// True when the command can currently run. Disabled commands are shown dimmed with a tooltip.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>Reason shown when <see cref="IsEnabled"/> is false. May be null.</summary>
        public string DisabledReason { get; set; }
    }
}
