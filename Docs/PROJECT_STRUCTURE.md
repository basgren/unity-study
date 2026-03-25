# Project Structure

Unity 2D pixel art platformer. All project-owned code and assets live under `Assets/Game/`.
The `Assets/` root is reserved for third-party content.

## Top-Level Layout
- `Assets/Game/` - All project-owned code, assets, and content.
- `Assets/TextMesh Pro/` - Third-party (imported by Unity, do not modify).
- `Packages/` - Unity package dependencies (`manifest.json`, `packages-lock.json`).
- `ProjectSettings/` - Unity project configuration.
- `Docs/` - Project documentation.
- `AGENTS.md`, `CLAUDE.md` - Repository-local agent/instruction files.

## Game/ Layout
- `Game/Core/` - Shared reusable systems (no feature-specific logic).
- `Game/Features/` - Self-contained gameplay features (feature-folder pattern).
- `Game/UI/` - Game UI (menus, HUD, widgets).
- `Game/Audio/` - Game-specific audio data (sound maps, profiles).
- `Game/Configs/` - Game-wide configuration (`MainConfig`, `PlayerConfig`).
- `Game/Defs/` - Generated definitions (`ItemIds`).
- `Game/Editor/` - General editor tools (not tied to one feature).
- `Game/Content/` - Centralized art and audio assets.
- `Game/Resources/` - Runtime-loadable assets (configs).
- `Game/Scenes/` - Scene files.
- `Game/System/` - App bootstrap and Input System config.

## Core/ (shared systems)
- `Core/Audio/` - Audio service, cues, interfaces.
- `Core/Components/` - Reusable gameplay components (animation, collisions, damage, interaction, camera, collectables, effects, scene management). Feature-specific editors live in local `Editor/` subfolders.
- `Core/FSM/` - State machine framework + `Editor/` for inspector.
- `Core/Models/` - Inventory, data models, `DefsFacade` + `Editor/` for drawers.
- `Core/Services/` - Global services:
  - `Bootstrap/` - `G`, `GInit`, `GameManager`, `AssetRefs`.
  - `Input/` - `InputService`.
  - `Scene/` - `SceneUtils`, `MenuManager`, `ScreenService`.
  - `SpawnerService`, `StateMachineService` at root.
- `Core/Tiles/` - Tilemap extensions (`PatternGridTile`).
- `Core/UI/` - Shared UI framework (`AnimatedWindow`, widgets).
- `Core/Utils/` - Helpers (`TinyTimer`, `SafePointTracker`, `Geometry`, `MultiRayCaster`).

## Features/ (gameplay feature folders)
Each subfolder is a complete feature bundle: scripts + prefab + animations + sounds + effects.
Feature-specific editor scripts live in local `Editor/` subfolders.

- `Features/Characters/` - Player and NPC characters.
  - `_Shared/` - `BaseCharacterController`, `BaseAI`, projectile bases, `GroundPatrolPath` + `Editor/`.
  - `Hero/` - Hero prefab, `PlayerController`, `PlayerState`, sound profiles, abilities, animations, projectiles, effects + `Editor/`.
  - `PinkStar/` - Pinky prefab, controller, AI, state machine, animations, sounds + `Editor/`.
  - `Sharky/` - Sharky prefab, controller, AI, state machine, animations, effects, sounds + `Editor/`.
- `Features/Hazards/ShootingTraps/` - Cannon, Seashell, Totem (with projectiles and sounds).
- `Features/Collectibles/` - Coins, Potions, Weapons.
- `Features/Doors/` - Door system (scripts, prefabs, editor validation suite).
- `Features/Interactive/` - Portal, Helm.
- `Features/Dynamic/` - Barrel (prefab + drag/highlight scripts).
- `Features/Props/` - TrainingDummy, destructible barrel, Chest.
- `Features/Effects/` - InfoBubble.
- `Features/Background/` - Clouds, Trees, Water, `CloudMover`.
- `Features/PirateShip/` - Ship decoration prefabs.
- `Features/GlobalRoot.prefab` - Shared scene root prefab.

## UI/
- `UI/MainMenu/` - Main menu screen + launcher.
- `UI/OptionsMenu/` - Options menu.
- `UI/PauseMenu/` - Pause menu.
- `UI/ModalAnim/` - Modal animation controller.
- `UI/Widgets/` - Reusable UI components (MenuButton, Slider, LabeledSlider) + `Editor/`.

## Editor/ (general tools)
- `ObjectBrush/` - Level design brush tool + profiles.
- `SpriteValidator/` - Batch sprite checks.
- `Tools/` - Batch rename, pivot tool, transition defaults.
- `Tests/FSM/` - Editor-side FSM tests.
- `SceneNote.cs`, `SceneNoteMenu.cs` - Scene annotation system.
- `SpritePivotBatchTool.cs` - Sprite pivot batch editor.
- `EditorConst.cs` - Shared editor constants.

## Content/ (centralized assets)
- `Content/Textures/` - Sprites organized by domain (Chars, Environment, Hazards, UI, etc.). Batch-exported from Aseprite.
- `Content/Music/` - Background music.
- `Content/Sounds/` - Shared SFX (feature-specific sounds live in feature folders).
- `Content/Materials/` - Physics materials.
- `Content/Tiles/` - Tile definitions.
- `Content/TilePalettes/` - Tile palettes (Outdoor, PirateShip).

## Third-Party / External
- Unity packages managed in `Packages/manifest.json` (includes `com.unity.inputsystem`, `com.unity.cinemachine`, `com.unity.textmeshpro`, 2D packages, `com.unity.2d.tilemap.extras`).
- `Assets/TextMesh Pro/` contains imported TMP assets/resources.

## Where to add new code

1. **Reusable system, no game-specific logic?** → `Game/Core/`
2. **Belongs to one feature?** → That feature's folder under `Game/Features/`
3. **Shared between features in the same category?** → `_Shared/` within that category
4. **UI (menus, HUD, widgets)?** → `Game/UI/`
5. **Game-wide config or data?** → `Game/Configs/`, `Game/Defs/`, or `Game/Audio/`
6. **Generic editor tool?** → `Game/Editor/`
7. **Feature-specific editor tool?** → `Editor/` subfolder inside that feature
8. **Third-party package?** → `Assets/` root (outside `Game/`)
9. **Sound clip for a feature?** → Feature folder's `Sounds/` subfolder
10. **Sprites/textures?** → `Game/Content/Textures/` (batch-exported, centralized)

## Risk Areas
- Scenes in `Game/Scenes/` and prefabs in feature folders are high-impact integration points.
- `Game/Resources/` is runtime-load sensitive (asset paths matter for `Resources.Load()`).
- Input setup lives in `Game/System/`; changes affect controls globally.
- Door system has cross-scene linkage — use door validation tools after changes.
- `Core/Services/Bootstrap/` holds global service entry points; changes have project-wide impact.
