# Project Structure
This is a Unity 2D project organized mainly under `Assets` with split areas for shared/core code, game-specific code, 
editor tooling, scenes, prefabs, and content assets.  
The layout below documents the current state only (no redesign assumptions).

## Top-Level Layout
- `Assets` - Main project code and content.
- `Packages` - Unity package dependencies (`manifest.json`, `packages-lock.json`).
- `ProjectSettings` - Unity project configuration.
- `UserSettings` - Local/editor user settings.
- `Library`, `Temp`, `Logs`, `obj` - Unity/generated build and cache data.
- `*.sln`, `*.csproj` - Generated project/IDE files for C# tooling.
- `AGENTS.md`, `CLAUDE.md` - Repository-local agent/instruction files.

## Assets Layout
- `Assets/Core` - Shared runtime foundation: components, services, audio abstractions, FSM, models, UI helpers, utilities.
- `Assets/Game` - Game-specific runtime logic and data types (player, doors, controllers, configs, UI, audio maps).
- `Assets/System` - App-level bootstrap and Unity Input System assets/code.
- `Assets/Editor` - Root editor scripts and tools.
- `Assets/Scenes` - Scene files (`IntroLevel`, `Level2SmallShip`, `Level3Island`, `MainMenuScene`, `TilesTest`, `_Template`).
- `Assets/Prefabs` - Prefab library grouped by gameplay/content category.
- `Assets/Resources` - Runtime-loadable assets (configs, refs/facades).
- `Assets/Textures`, `Assets/Tiles`, `Assets/TilePalettes`, `Assets/Materials`, `Assets/Sounds`, `Assets/Music` - Art/audio/tile content.
- `Assets/TextMesh Pro` - TextMesh Pro local asset content.

## Core Runtime Areas
- `Assets/Core/Components` - Reusable gameplay components (animation, collisions, damage, interaction, camera, collectables, scene management).
- `Assets/Core/Services` - Shared services and global access points (`GameManager`, `InputService`, `MenuManager`, `Audio` service types, `G`/`GInit`).
- `Assets/Core/FSM` - State machine implementation plus editor inspector under `Assets/Core/FSM/Editor`.
- `Assets/Core/Models` - Data models and inventory definitions; includes editor utilities in `Assets/Core/Models/Editor`.
- `Assets/Game` - Feature-level gameplay logic (notably `Assets/Game/Player`, `Assets/Game/Doors`, `Assets/Game/Components/Abilities`, `Assets/Game/Controllers`, `Assets/Game/UI`).
- `Assets/Prefabs/.../*.cs` - Some prefab-local behavior scripts are stored beside prefab assets (for example characters, hazards, props).  
  Likely purpose: keep prefab-specific logic close to prefab content.

## Editor / Tooling
- `Assets/Editor` - General editor extensions (inspectors, sprite tools, object brush, scene note tooling, tests).
- `Assets/Game/Doors/Editor` - Door/link validation and repair tooling tied to door workflow.
- `Assets/Game/UI/Widgets/Editor` and prefab-local `Editor` folders - Feature-specific custom inspectors/drawers.
- `Assets/Editor/Tests/FSM` - Editor-side test script location.

## Content / Data
- `Assets/Textures` - Sprite/art source grouped by domain (`Chars`, `Environment`, `Hazards`, `UI`, etc.).
- `Assets/Tiles` and `Assets/TilePalettes` - Tile assets and palette assets (`Outdoor`, `PirateShip`).
- `Assets/Materials` - Physics materials (`*.physicsMaterial2D`).
- `Assets/Sounds` and `Assets/Music` - Audio clips and grouped SFX categories.
- `Assets/Resources/Configs` - Runtime-loadable config assets (`MainConfig.asset`, `PlayerConfig.asset`).
- `Assets/Resources` - Additional shared runtime assets (`AssetRefs.asset`, `DefsFacade.asset`).
- `Assets/Game/Defs` - Definition assets/classes (for example inventory definitions).
- `Assets/Game/Audio` and prefab sound folders - Audio cue/profile assets (`*.asset`) mapped to gameplay events.

## Scenes and Prefabs
- `Assets/Scenes` - Main scenes and a template scene. Likely purpose based on names: menu + level flow.
- `Assets/Prefabs` - Large prefab catalog, including `Assets/Prefabs/Background`, `Assets/Prefabs/Characters`,
  `Assets/Prefabs/Collectibles`, `Assets/Prefabs/Dynamic`, `Assets/Prefabs/Effects`, `Assets/Prefabs/Hazards`,
  `Assets/Prefabs/Interactive`, `Assets/Prefabs/PirateShip`, `Assets/Prefabs/Props`.
- `Assets/Prefabs/GlobalRoot.prefab` exists.  
  Assumption: likely a shared/global composition prefab used across scenes.

## Third-Party / External
- Unity packages are managed in `Packages/manifest.json` (includes `com.unity.inputsystem`, `com.unity.cinemachine`,
  `com.unity.textmeshpro`, 2D packages, and `com.unity.2d.tilemap.extras` from GitHub).
- `Assets/TextMesh Pro` contains imported TMP assets/resources.
- No `Assets/Plugins` directory was found in current structure.

## Where to add new code
- New gameplay script: prefer `Assets/Game/<feature>` (for feature-specific logic) or
  `Assets/Core/Components`/`Assets/Core/Services` (for reusable/shared logic).
- UI logic: prefer `Assets/Game/UI/<feature>`; use `Assets/Core/UI` for shared UI utilities/widgets.
- Editor tooling: prefer `Assets/Editor` for generic tools; use feature-local `Editor` folders (for example
  `Assets/Game/Doors/Editor`) when tooling is tightly coupled to one feature.
- Config/data assets: prefer `Assets/Resources/Configs` for runtime-loaded config assets, and existing domain
  folders like `Assets/Game/Defs` for definition assets.
- Avoid placing new code in pure content folders such as `Assets/Textures`, `Assets/Sounds`, `Assets/Music`,
  `Assets/Tiles`, `Assets/TilePalettes`, or vendor content under `Assets/TextMesh Pro` unless there is a strong
  project-specific reason.

## Notes / Risk Areas
- Scenes in `Assets/Scenes` and prefabs in `Assets/Prefabs` are high-impact integration points; reference changes
  can ripple quickly.
- `Assets/Resources` is runtime-load sensitive (asset names/paths matter for `Resources` loading).
- Input setup lives in `Assets/System` (`InputActions.inputactions`, generated `InputActions.cs`,
  `InputSystem.inputsettings.asset`); changes here can affect controls globally.
- Door system has dedicated validators/repair tools under `Assets/Game/Doors/Editor`; this suggests cross-scene
  linkage/data integrity concerns.
- `Assets/Core/Services` appears to hold global service entry points.  
  Assumption: changes here may have broad project-wide runtime impact.
