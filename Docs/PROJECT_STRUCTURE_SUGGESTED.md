# Suggested Project Structure

This document defines the target folder layout for the project and a plan to migrate
toward it incrementally. The project started without a clear structure and was improved
during development. This document captures the desired end state.

Nothing should be applied blindly — each move carries Unity-specific risks
(broken prefab/scene references, lost `.meta` GUIDs, etc.).

---

## Guiding Principles

### 1. Game-root isolation
All project-owned code and assets live under `Assets/Game/`. The `Assets/` root is
reserved for third-party content (Asset Store imports, TextMesh Pro, future plugins).
This gives a clean boundary: everything under `Game/` is ours, everything else is not.

### 2. Feature folders
Everything a feature needs — prefab, scripts, animations, sounds, effects, editor
tools — lives together in one folder. Delete the folder and the feature is fully removed.

### 3. Asset placement by type
Feature folders hold scripts, prefabs, animations (.anim/.controller), sounds, and
ScriptableObject data (.asset). Sprites and textures stay centralized because they are
batch-exported from art tools (Aseprite) and consist of many files per feature.

| Asset type | Where | Why |
|---|---|---|
| Scripts, prefabs, animations, sounds, SO data | Feature folder | Tightly coupled to one feature, added/tuned one at a time |
| Sprites / textures | `Game/Content/Textures/` | Batch-exported from Aseprite, many files per feature |
| Music | `Game/Content/Music/` | Global, not feature-owned |
| Materials | `Game/Content/Materials/` | Shared by nature |
| Tiles, tile palettes | `Game/Content/Tiles/`, `Game/Content/TilePalettes/` | Shared tilemap assets |

### 4. Placement rules
- Script or asset owned by **one feature** → that feature's folder.
- Script or asset **shared across features in the same category** → `_Shared/` subfolder
  within that category (e.g., `Features/Characters/_Shared/`).
- Script or asset **shared across the whole project** → `Core/`.
- **Game-wide infrastructure** (bootstrap, configs, input) → `Core/` or `System/`.

---

## What Works Today

1. **Pinky and Sharky already follow the feature-folder pattern** — scripts, prefab,
   animations, sounds, and editor tools all live together. This is the model to follow.

2. **Feature categories are sensible** — Characters, Hazards, Collectibles,
   Interactive, Props. Good grouping.

3. **Feature-local Editor/ folders** — `Game/Doors/Editor/`, `Core/FSM/Editor/`,
   `Prefabs/Characters/PinkStar/Editor/`. This Unity convention is worth keeping.

4. **Content folders are well-organized** — `Textures/`, `Sounds/`, `Music/`, `Tiles/`,
   `TilePalettes/` each have sensible internal grouping.

5. **Resources/ is lean** — only configs and a few facade assets.

6. **Core/ has useful shared systems** — Audio, FSM, Components, Utils.

---

## Current Problems

### P1: No boundary between our code and third-party
`Assets/` root mixes project folders (`Core/`, `Game/`, `Prefabs/`, `Scenes/`, etc.)
with third-party content (`TextMesh Pro/`). Any future Asset Store import will land at
the same level, making it harder to tell what's ours.

### P2: Inconsistent placement of feature code

| Feature | Scripts | Prefab/Assets |
|---|---|---|
| Hero | `Game/Player/`, `Game/Components/Abilities/`, `Game/Controllers/` | `Prefabs/Characters/Hero/` |
| Pinky | `Prefabs/Characters/PinkStar/` | `Prefabs/Characters/PinkStar/` |
| Sharky | `Prefabs/Characters/Sharky/` | `Prefabs/Characters/Sharky/` |
| Cannon | `Prefabs/Hazards/ShootingTraps/Cannon/` | same |
| Doors | `Game/Doors/` | `Prefabs/Interactive/StoneDoor/` (separate) |

Pinky and Sharky are correct. Hero is scattered across three folders. Doors are split.

### P3: Shared character code is split
- `BaseCharacterController.cs` is in `Game/Controllers/` (alone).
- `BaseAI.cs`, projectile bases are in `Prefabs/Characters/Common/`.
- Same purpose, different locations.

### P4: Orphan and single-file folders
- `Game/Background/` — one file (`CloudMover.cs`).
- `Game/EditorOnly/` — one file (`SceneNote.cs`).
- `Game/Models/` — one file (`PlayerState.cs`), player-specific.
- `Game/Controllers/` — one file (`BaseCharacterController.cs`).
- `Game/Components/Abilities/` — four files, all player-specific.

### P5: Services/ is a flat grab-bag
`Core/Services/` has 10 files mixing bootstrap singletons, utility services, scene
helpers, and asset loading. No subfolder structure.

### P6: `Game/` vs `Prefabs/` boundary is unclear
Both hold gameplay code. With feature folders, the split creates ambiguity about
where new code should go.

### P7: No assembly definitions
All runtime code compiles into `Assembly-CSharp.dll`. No `.asmdef` files to enforce
layer boundaries or speed up recompilation.

---

## Recommended Target Structure

```
Assets/
│
├── Game/                                  # All project-owned code and assets
│   │
│   ├── Core/                              # Shared reusable systems (no feature-specific logic)
│   │   ├── Audio/                         #   AudioService, AudioCue, IAudioService
│   │   ├── Components/                    #   Shared gameplay components
│   │   │   ├── Animation/                 #     SimpleSpriteAnimator, MultiStateSpriteAnimator
│   │   │   ├── Base2D/                    #     Facing2D
│   │   │   ├── Behavior/                  #     AutoTrigger
│   │   │   ├── Camera/                    #     ParallaxScroller
│   │   │   ├── Collectables/              #     Collectable, PowerUpComponent, DelayedPickup
│   │   │   ├── Collisions/               #     GroundCheckComponent, LayerCheck, TriggerEnter
│   │   │   ├── Damage/                    #     Damageable, Damager
│   │   │   ├── Effects/                   #     DebrisController, ItemFloater
│   │   │   ├── Extensions/               #     LayerMaskExtensions
│   │   │   ├── GameObjects/              #     DestroyObjectComponent, LootDropper, SpawnComponent
│   │   │   ├── Interaction/              #     InteractableBase, Switch, Switchable
│   │   │   └── SceneManagement/          #     ReloadLevelComponent
│   │   ├── FSM/                           #   State machine framework
│   │   │   └── Editor/                    #     RuntimeStateMachineInspector
│   │   ├── Models/                        #   Inventory, data models, DefsFacade
│   │   │   └── Editor/                    #     InventoryDrawer, ItemIdDrawer, ItemIdsGenerator
│   │   ├── Services/                      #   Global services
│   │   │   ├── Bootstrap/                 #     G, GInit, GameManager, AssetRefs
│   │   │   ├── Input/                     #     InputService
│   │   │   └── Scene/                     #     SceneUtils, MenuManager, ScreenService
│   │   │   (SpawnerService, StateMachineService stay at Services/ root)
│   │   ├── Tiles/                         #   PatternGridTile
│   │   ├── UI/                            #   AnimatedWindow, shared widgets
│   │   └── Utils/                         #   TinyTimer, SafePointTracker, Geometry, MultiRayCaster
│   │
│   ├── Features/                          # Self-contained gameplay features
│   │   │                                  # Each subfolder = complete feature bundle:
│   │   │                                  # scripts + prefab + animations + sounds + effects
│   │   │
│   │   ├── Characters/
│   │   │   ├── _Shared/                   #   BaseCharacterController, BaseAI, projectile bases
│   │   │   │   ├── BaseCharacterController.cs
│   │   │   │   ├── BaseAI.cs
│   │   │   │   ├── GroundPatrolPath.cs
│   │   │   │   ├── ProjectileBase.cs
│   │   │   │   ├── LinearProjectile.cs
│   │   │   │   ├── WobblingProjectile.cs
│   │   │   │   └── Effects/               #     RunDust.prefab
│   │   │   ├── Hero/
│   │   │   │   ├── Hero.prefab
│   │   │   │   ├── PlayerController.cs
│   │   │   │   ├── PlayerState.cs
│   │   │   │   ├── PlayerSoundProfile.cs
│   │   │   │   ├── PlayerSoundProfileLink.cs
│   │   │   │   ├── HeroAttackStateBehavior.cs
│   │   │   │   ├── EmbeddedSword.cs
│   │   │   │   ├── SpinningSword.cs
│   │   │   │   ├── Abilities/              #     DragAbility, WalkUpstairsAbility, etc.
│   │   │   │   ├── Animations/             #     Armed/, Unarmed/ controllers and clips
│   │   │   │   ├── Projectiles/            #     EmbeddedSword.prefab, SpinningSword.prefab
│   │   │   │   ├── Effects/                #     HeroJumpDust, HeroFallDust, SwordAttackEffects
│   │   │   │   └── Sounds/
│   │   │   ├── Pinky/
│   │   │   │   ├── Pinky.prefab
│   │   │   │   ├── PinkyController.cs
│   │   │   │   ├── PinkyAI.cs
│   │   │   │   ├── PinkyStateMachine.cs
│   │   │   │   ├── PinkyAttackStateBehavior.cs
│   │   │   │   ├── BaseControlSource.cs
│   │   │   │   ├── PinkyInputControlSource.cs
│   │   │   │   ├── Anims/
│   │   │   │   ├── Sounds/
│   │   │   │   └── Editor/                 #     PinkyAIInspector, PinkyControllerInspector
│   │   │   └── Sharky/
│   │   │       ├── Sharky.prefab
│   │   │       ├── SharkyController.cs
│   │   │       ├── SharkyAI.cs
│   │   │       ├── SharkyStateMachine.cs
│   │   │       ├── SharkyAttackStateBehavior.cs
│   │   │       ├── Animations/
│   │   │       ├── Effects/
│   │   │       └── Sounds/
│   │   │
│   │   ├── Hazards/
│   │   │   ├── _Shared/                    #   SimpleShooter, SimpleShooterAI
│   │   │   ├── Cannon/
│   │   │   │   ├── Cannon.prefab
│   │   │   │   ├── CannonAI.cs
│   │   │   │   ├── CannonController.cs
│   │   │   │   └── Projectiles/
│   │   │   ├── Seashell/
│   │   │   │   ├── Seashell.prefab
│   │   │   │   ├── SeashellAI.cs
│   │   │   │   ├── SeashellController.cs
│   │   │   │   ├── SeashellAnimEvents.cs
│   │   │   │   ├── Projectiles/
│   │   │   │   └── Sounds/
│   │   │   └── Totem/
│   │   │       ├── BigMouthTotem/
│   │   │       ├── BirdTotem/
│   │   │       ├── SpitterTotem/
│   │   │       └── Projectiles/
│   │   │
│   │   ├── Collectibles/
│   │   │   ├── Coins/
│   │   │   ├── Potions/
│   │   │   └── Weapons/
│   │   │
│   │   ├── Doors/
│   │   │   ├── Door.cs
│   │   │   ├── DoorLink.cs
│   │   │   ├── DoorTravelService.cs
│   │   │   ├── DoorIdUtils.cs
│   │   │   ├── DoorUtils.cs
│   │   │   ├── SceneReference.cs
│   │   │   ├── StoneDoor/                  #   StoneDoor.prefab + animations
│   │   │   ├── DoorWooden/                 #   DoorWooden.prefab + animations
│   │   │   └── Editor/                     #   Door validation/repair suite (12 scripts)
│   │   │
│   │   ├── Interactive/
│   │   │   ├── Portal/                     #   Portal.prefab, PortalController.cs, etc.
│   │   │   ├── Helm/                       #   HelmController.cs + prefab
│   │   │   ├── Barrel/                     #   Barrel.prefab, DraggableBarrel.cs, etc.
│   │   │   └── Chest/                      #   Chest.prefab, ChestController.cs
│   │   │
│   │   ├── Props/
│   │   │   ├── TrainingDummy/              #   TrainingDummy.prefab + controller
│   │   │   └── BarrelDestructable/         #   BarrelDestructable.prefab + debris
│   │   │
│   │   ├── Effects/
│   │   │   └── InfoBubble/                 #   InfoBubble.prefab, InfoBubble.cs
│   │   │
│   │   └── Background/
│   │       ├── CloudMover.cs
│   │       ├── Clouds/
│   │       ├── Trees/
│   │       └── Water/
│   │
│   ├── UI/                                # Game UI (menus, HUD, widgets)
│   │   ├── MainMenu/                      #   MainMenu.cs, MainMenuLauncher.cs
│   │   ├── OptionsMenu/                   #   OptionsMenu.cs
│   │   ├── PauseMenu/                     #   PauseMenu.cs
│   │   ├── ModalAnim/                     #   Modal animation controller
│   │   └── Widgets/                       #   MenuButton, Slider, LabeledSlider
│   │       └── Editor/                    #     MenuButtonEditor
│   │
│   ├── Audio/                             # Game-specific audio data
│   │   ├── CommonSoundMap.cs
│   │   └── Profiles/
│   │
│   ├── Configs/                           # Game-wide configuration
│   │   ├── MainConfig.cs
│   │   └── PlayerConfig.cs
│   │
│   ├── Defs/                              # Generated definitions
│   │   └── ItemIds.cs
│   │
│   ├── Editor/                            # General editor tools (not tied to one feature)
│   │   ├── Characters/                    #   GroundPatrolPathEditor
│   │   ├── Inspectors/                    #   Cross-feature inspectors
│   │   ├── ObjectBrush/                   #   Level design brush tool
│   │   ├── SpriteValidator/               #   Batch sprite checks
│   │   ├── Tests/                         #   Editor tests
│   │   └── Tools/                         #   Batch rename, pivot tool, etc.
│   │
│   ├── Content/                           # Centralized art/audio not owned by one feature
│   │   ├── Materials/                     #   Physics materials
│   │   ├── Music/                         #   Background music
│   │   ├── Textures/                      #   Sprites organized by domain (batch-exported)
│   │   ├── Tiles/                         #   Tile definitions
│   │   └── TilePalettes/                 #   Tile palettes (Outdoor, PirateShip)
│   │
│   ├── Resources/                         # Runtime-loaded assets (keep lean)
│   │   └── Configs/                       #   MainConfig.asset, PlayerConfig.asset
│   │
│   ├── Scenes/                            # Scene files
│   │
│   ├── System/                            # App bootstrap, input config
│   │
│   └── GlobalRoot.prefab                  # Shared scene root prefab
│
├── TextMesh Pro/                          # Third-party (auto-installed, do not modify)
└── ...                                    # Future Asset Store imports land here harmlessly
```

### Key decisions

**`Game/` wraps all project-owned content.**
Third-party imports (Asset Store, plugins) land at `Assets/` root and cannot mix with
our code. At a glance you can see what's ours (`Game/`) and what's not.

**`Features/` replaces `Prefabs/`.**
The folder holds complete feature bundles (scripts + prefabs + assets), not just prefabs.
The name reflects that.

**`_Shared/` for category-level shared code.**
Within `Features/Characters/`, shared code lives in `_Shared/` (sorts to top in file
browsers). Replaces the current `Common/` naming.

**`UI/` is a top-level folder inside `Game/`.**
UI is cross-cutting — menus, HUD, and widgets don't belong to one gameplay feature.

**`Content/` groups pure asset folders.**
`Materials/`, `Music/`, `Sounds/`, `Textures/`, `Tiles/`, `TilePalettes/` all live under
`Game/Content/`. Reduces clutter inside `Game/`.

**Sounds live in feature folders. Sprites stay centralized.**
Sound clips are found and processed one at a time for specific gameplay events — they
belong next to the feature they serve (e.g., `Features/Characters/Sharky/Sounds/`).
Sprites are batch-exported from Aseprite as dozens of frames per animation state —
centralizing them in `Content/Textures/` keeps art workflows clean and avoids cluttering
code folders. Truly shared sounds (UI clicks, ambient) can live in `Game/Content/Sounds/`
if needed, but most sounds are feature-owned.

**`Resources/` works inside `Game/`.**
`Resources.Load()` works relative to any folder named `Resources/` anywhere in `Assets/`,
so `Game/Resources/` is fully supported.

---

## Refactoring Principles

1. **Feature owns its code and assets.**
   If a script or asset serves one feature, it lives in that feature's folder.

2. **Shared code has an explicit home.**
   Cross-feature code lives in `Core/` (framework-level) or `_Shared/` (category-level).
   Never in a random feature folder that happens to use it first.

3. **One pattern per concept.**
   All characters follow the same folder pattern. All hazards follow the same pattern.

4. **Feature-local Editor/ folders are encouraged.**
   Keep inspectors and drawers next to the code they serve. Only put editor scripts
   in the root `Game/Editor/` if they're truly generic tools.

5. **Move files with `git mv`** to preserve history and `.meta` GUIDs.

6. **Move one domain at a time**, verify in Unity, commit, then move the next.

7. **Every move must preserve `.meta` GUIDs.** Unity tracks assets by GUID. If a `.meta`
   file is lost or regenerated, all references break.

---

## Incremental Migration Plan

Each phase is a standalone commit (or small PR). Verify in Unity Editor after each phase.

### Phase 1: Create the Game-root wrapper
Move all project-owned top-level folders under `Game/`. This is the foundation for
everything else.

Current `Game/` already has content (`Audio/`, `Doors/`, `Player/`, `UI/`, etc.).
The other project folders move into it as siblings:

| From | To |
|---|---|
| `Assets/Core/` | `Assets/Game/Core/` |
| `Assets/Prefabs/` | `Assets/Game/Prefabs/` |
| `Assets/Editor/` | `Assets/Game/Editor/` |
| `Assets/Scenes/` | `Assets/Game/Scenes/` |
| `Assets/System/` | `Assets/Game/System/` |
| `Assets/Resources/` | `Assets/Game/Resources/` |
| `Assets/Textures/` | `Assets/Game/Content/Textures/` |
| `Assets/Sounds/` | `Assets/Game/Content/Sounds/` (temporary — clips migrate to feature folders over time) |
| `Assets/Music/` | `Assets/Game/Content/Music/` |
| `Assets/Materials/` | `Assets/Game/Content/Materials/` |
| `Assets/Tiles/` | `Assets/Game/Content/Tiles/` |
| `Assets/TilePalettes/` | `Assets/Game/Content/TilePalettes/` |

**Leave in place:** `Assets/TextMesh Pro/` (third-party).

**Risk:** High — this is the largest single move. Every `.meta` file must travel with
its asset. Use `git mv` for each folder. Open Unity after the move and let it reimport.
Check for missing references across all scenes and prefabs.

**Tip:** Do this in one commit. Moving half the folders and leaving the other half
creates a broken intermediate state.

### Phase 2: Consolidate Hero into its feature folder
Hero is the most scattered feature. Bring all Hero code together.

| From | To |
|---|---|
| `Game/Player/PlayerController.cs` | `Game/Prefabs/Characters/Hero/PlayerController.cs` |
| `Game/Player/PlayerSoundProfile.cs` | `Game/Prefabs/Characters/Hero/PlayerSoundProfile.cs` |
| `Game/Player/PlayerSoundProfileLink.cs` | `Game/Prefabs/Characters/Hero/PlayerSoundProfileLink.cs` |
| `Game/Models/PlayerState.cs` | `Game/Prefabs/Characters/Hero/PlayerState.cs` |
| `Game/Components/Abilities/*.cs` (4 files) | `Game/Prefabs/Characters/Hero/Abilities/*.cs` |

**Risk:** Medium. Scripts are referenced by the Hero prefab and scene objects. GUID
preservation keeps references intact. Verify Hero prefab and all scenes.

### Phase 3: Consolidate shared character code

| From | To |
|---|---|
| `Game/Controllers/BaseCharacterController.cs` | `Game/Prefabs/Characters/Common/BaseCharacterController.cs` |

`Prefabs/Characters/Common/` already has `BaseAI.cs`, projectile bases, and
`GroundPatrolPath.cs` — this adds the missing piece.

**Risk:** Low. One file, widely referenced but GUID-tracked.

### Phase 4: Consolidate Doors into a feature folder
Move door prefabs to join the door scripts, and move everything under `Features/`.

| From | To |
|---|---|
| `Game/Doors/*.cs` + `Game/Doors/Editor/` | `Game/Prefabs/Doors/` (scripts + editor) |
| `Game/Prefabs/Interactive/StoneDoor/` | `Game/Prefabs/Doors/StoneDoor/` |
| `Game/Prefabs/PirateShip/DoorWooden/` | `Game/Prefabs/Doors/DoorWooden/` |

**Risk:** Medium. Door system has cross-scene references and a large editor tooling
suite. Run door validation after the move.

### Phase 5: Move orphans and remaining scattered code

| From | To |
|---|---|
| `Game/Background/CloudMover.cs` | `Game/Prefabs/Background/CloudMover.cs` |
| `Game/EditorOnly/SceneNote.cs` | `Game/Editor/SceneNote.cs` |
| `Game/UI/MainMenuLauncher.cs` | `Game/UI/MainMenu/MainMenuLauncher.cs` |

Verify that `DraggableBarrel.cs` and `BarrelHighlighter.cs` (in `Game/Components/Abilities/`)
are Hero-specific or Barrel-specific:
- If Barrel-specific → `Game/Prefabs/Interactive/Barrel/`
- If Hero-specific → they already moved in Phase 2

Delete empty folders: `Game/Player/`, `Game/Controllers/`, `Game/Components/`,
`Game/Models/`, `Game/Background/`, `Game/EditorOnly/`.

**Risk:** Low.

### Phase 6: Rename Prefabs/ to Features/
Now that feature folders contain complete bundles (not just prefabs), rename to match.

| From | To |
|---|---|
| `Game/Prefabs/` | `Game/Features/` |

Also rename `Common/` to `_Shared/` within each category if desired.

**Risk:** Medium. Single `git mv` on a folder with many children. Verify in Unity.

### Phase 7: Light Services/ restructuring
Add subfolders inside `Game/Core/Services/`:

```
Core/Services/
├── Bootstrap/              # G.cs, GInit.cs, GameManager.cs, AssetRefs.cs
├── Input/                  # InputService.cs
├── Scene/                  # SceneUtils.cs, MenuManager.cs, ScreenService.cs
├── SpawnerService.cs
└── StateMachineService.cs
```

**Risk:** Low. Code-only, no serialized references.

### Phase 8 (optional): Assembly Definitions
Add `.asmdef` files to enforce the dependency direction:
- `Game/Core/Core.asmdef` — no references to Features
- `Game/Features/Features.asmdef` — references Core only
- `Game/Editor/Editor.asmdef` — editor platform only, references Core + Features

**Risk:** Medium. May surface hidden cross-layer dependencies as compile errors.
High value for long-term maintainability.

---

## Unity-Specific Risk Areas

| Risk | Detail | Mitigation |
|---|---|---|
| **Broken prefab references** | Prefabs store script refs by GUID from `.meta` files | Always move `.cs` + `.meta` together via `git mv`; never delete and recreate |
| **Broken animator states** | Animator controllers reference `StateMachineBehaviour` by GUID | Verify each animator controller after moving state behavior scripts |
| **Broken scene references** | Scenes reference scripts on GameObjects by GUID | Open every scene after each phase and check for "Missing Script" warnings |
| **Resources path changes** | `Resources.Load()` uses path relative to any `Resources/` folder | `Game/Resources/` works the same as `Assets/Resources/` — no code changes needed |
| **Animation events** | `.anim` clips reference methods by name on MonoBehaviours | Method names don't change when moving files, but verify |
| **Editor script placement** | Unity only compiles `Editor/` contents for editor platform | Ensure editor scripts always land inside an `Editor/` named folder |
| **Door cross-scene links** | Door system has validators because links span scenes | Run door validation after moving door files |
| **ScriptableObject assets** | `.asset` files reference their script class by GUID | Moving `.cs` + `.meta` preserves the link, but verify inspector shows data |
| **Large batch move (Phase 1)** | Moving many folders at once increases risk of a missed `.meta` | Do Phase 1 in one commit; use `git status` to verify no orphaned `.meta` files |

---

## Deciding Where New Code Goes

When adding something new, follow this flow:

1. **Is it a reusable system with no game-specific logic?**
   → `Game/Core/` (appropriate subfolder)

2. **Does it belong to one specific feature (character, hazard, prop, etc.)?**
   → That feature's folder under `Game/Features/`

3. **Is it shared between features in the same category?**
   → `_Shared/` within that category (e.g., `Game/Features/Characters/_Shared/`)

4. **Is it UI (menus, HUD, widgets)?**
   → `Game/UI/`

5. **Is it game-wide configuration or data?**
   → `Game/Configs/`, `Game/Defs/`, or `Game/Audio/`

6. **Is it a generic editor tool?**
   → `Game/Editor/`

7. **Is it a feature-specific editor tool?**
   → `Editor/` subfolder inside that feature

8. **Is it a third-party package or Asset Store import?**
   → `Assets/` root (outside `Game/`)

---

## Final Recommendation

**Phase 1 (Game-root wrapper) is the structural foundation.** It's the largest single
move but establishes the clean boundary between our code and third-party content.
Do it first, verify thoroughly, then proceed with feature consolidation.

**Phases 2-5 (feature consolidation)** are the highest-value content changes. They
fix the inconsistencies and establish the feature-folder pattern. Each is focused
and independently verifiable.

**Phase 6 (rename to Features/)** is cosmetic but clarifies intent. Low effort.

**Phases 7-8** are polish. Do them when convenient.

After each phase, the project should compile and run correctly. If something breaks,
the cause is almost always a lost `.meta` file — check `git status` for untracked
`.meta` files that should have been moved with their counterpart.