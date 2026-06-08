# Create Plus

**Create Plus is an additional creation palette for the Unity Editor. It does not remove or replace
Unity's built-in Create menu** — it sits alongside it and offers a faster, searchable, configurable
way to create assets.

Unity's standard Create menu grows into a huge vertical list once packages and project-specific items
are added, making common commands (Folder, C# Script, Material, Scene, ScriptableObjects, etc.) hard to
find. Create Plus groups those commands into compact panels with favorites, pinning, search and
remembered preferences.

## What it does

- Groups create commands into three panels: **Quick Access**, **Project**, and **Unity Common**.
- **Favorites** — star any command to keep it in Quick Access.
- **Pinning** — pin important commands so they stay visible even when their group is collapsed.
- **Search** — filter commands by name, original menu path, group, source, kind or alias.
- **Recent** — the last few executed commands (collapsed by default).
- Remembers collapsed/expanded groups, favorites, pins, hidden commands and usage counts per user.
- Creates new assets inside the folder selected in the Project window, then selects and pings them.

## How to open

- **Project window**: right-click in the Project view and choose **Create Plus** (top of the menu).
- **Main menu**: **Tools ▸ Create Plus ▸ Open**.
- **Keyboard shortcut**: **Ctrl+Alt+N** (Cmd+Alt+N on macOS).
  *Ctrl+Shift+N is intentionally not used because Unity already binds it to "Create Empty Child".*

The palette opens as a single borderless popup near the center of the editor. It closes on **Esc**, on
**successful command execution**, and when it loses focus (outside click).

### Keyboard

- The search field is focused on open.
- **Up/Down** moves the selection through visible commands.
- **Enter** runs the selected command (or the first match while searching).
- **Esc** closes the palette.

### Row actions

Hover a command row to reveal icon buttons (tooltips included):

- ☆ / ★ — Add to / Remove from Favorites
- 📌 — Pin / Unpin in group
- ⋮ — More: favorite, pin, hide, show original Unity path, reset command settings

The gear button next to the search field opens settings (show hidden commands, reset all settings).

## Current limitations (MVP)

- Real execution is implemented for: **Folder, C# Script, Material, Scene, Text, Assembly Definition,
  Assembly Definition Reference**.
- All other commands are **registered placeholders**: they are visible and searchable, and selecting
  one logs `Create Plus command is registered but execution is not implemented yet: <path>` without
  closing the palette. No command is ever silently dropped.
- The command list is a **static registry**; automatic discovery (MenuItem scanning, CreateAssetMenu
  attributes, project providers) is not implemented yet.
- Project-window usage only. Hierarchy and Scene View entry points are planned.
- UI is IMGUI (`CreatePlusWindowIMGUI`). A UI Toolkit window can be added later (see below).

## How settings are stored

User preferences are serialized to JSON and stored in **EditorPrefs** under the key
`CreatePlus.Settings.v1` (see `CreatePlusSettingsStore`). They include favorites, pinned commands,
hidden commands, collapsed/expanded group overrides, recent commands and usage counters.

To wipe them, use the gear menu ▸ **Reset All Create Plus Settings**.

A later version can migrate this to `UserSettings/CreatePlus.user.json` and/or a shared ScriptableObject
without changing callers — only the store's persistence methods change.

## How to add commands / providers later

Implement `ICreatePlusCommandProvider` and register it before the palette is first opened:

```csharp
using CreatePlus.Core;

[UnityEditor.InitializeOnLoad]
static class MyCreatePlusCommands {
    static MyCreatePlusCommands() {
        CreatePlusCommandRegistry.RegisterProvider(new MyProvider());
    }
}

class MyProvider : ICreatePlusCommandProvider {
    public System.Collections.Generic.IEnumerable<CreatePlusCommand> GetCommands() {
        yield return new CreatePlusCommand {
            Id = "project.my-thing",          // stable, unique id (never the display name)
            DisplayName = "My Thing",
            PanelName = "Project",
            GroupName = "Game",
            OriginalPath = "Assets/Create/My Thing",
            Kind = CreatePlusCommandKind.CustomFactory,
            IsImplemented = true,
            Execute = ctx => { /* create something in ctx.TargetFolderAssetPath */ },
            Aliases = new[] { "thing" },
            Source = "MyProject",
        };
    }
}
```

The built-in commands live in `Editor/Commands/CreatePlusBuiltInCommands.cs` and
`CreatePlusProjectCommands.cs` and follow the same pattern.

## Architecture

The core is intentionally **UI-independent** — it has no dependency on IMGUI or UI Toolkit:

- `Editor/Core/` — command model, kind, context, registry, provider interface, settings + store,
  filter/search, executor.
- `Editor/Commands/` — static command providers and concrete asset-creation routines.
- `Editor/UI/` — `CreatePlusViewModel` (builds a renderable model from the core) and the IMGUI view
  (`CreatePlusWindowIMGUI`, `CreatePlusStyles`, `CreatePlusIcons`).

`CreatePlusViewModel` is the bridge: it resolves favorites, pins, hidden, collapsed and recent state
into plain data that any view can draw. A future `CreatePlusWindowUIToolkit` can reuse the registry,
settings, context, filtering, view model and executor unchanged.

See [`Docs/architecture.md`](Docs/architecture.md) for the full architecture (with C4 diagrams) and
[`Docs/plan.md`](Docs/plan.md) for the phased implementation plan.

## Moving it to a UPM package later

Everything lives under `Assets/Editor/CreatePlus/` with its own editor-only assembly definition
(`CreatePlus.asmdef`, assembly name `CreatePlus.Editor`) and no runtime or third-party dependencies.

To extract it as a package:

1. Create `Packages/com.company.create-plus/`.
2. Add a `package.json` (name `com.company.create-plus`, an editor-only layout).
3. Move `Editor/` and the asmdef there (move each `.meta` together with its file so GUIDs are kept).
4. Set the namespace/company as desired.

Because the assembly is already isolated, no project code references it and it references nothing
project-specific, so extraction is a move rather than a refactor.
