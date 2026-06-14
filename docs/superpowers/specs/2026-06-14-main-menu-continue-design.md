# Main Menu: Continue / New Game

## Goal
When the player exits an in-progress game to the main menu, the menu should offer
**Continue** and **New Game** instead of a single "Start Game" button.

- **Continue** — resumes the game from the last resting point (bonfire checkpoint).
- **New Game** — completely resets the current game and starts from the very beginning.
- Continue is shown only when a saved resting point exists.

## Scope
- **In-session only.** Save state lives in `DontDestroyOnLoad` services and survives
  exiting to the main menu within the same app run. It is *not* persisted to disk, so
  after a full app quit + relaunch only **New Game** is shown. Disk persistence across
  app restarts is explicitly **out of scope**.

## Background: how saving works today
The save system is entirely in-memory (see `docs/system/state-saving.md`):

- `CheckpointService` (DontDestroyOnLoad) tracks the active checkpoint `Current` and the
  set of `discovered` bonfires. A resting point is created when the player rests at a
  bonfire (`Bonfire.DoInteract` → `CheckpointService.Activate`).
- `SceneStateService` (DontDestroyOnLoad) holds per-scene world state in two tiers:
  `sessionStore` (cleared on bonfire rest) and `persistentStore` (survives rest).
- `GameManager.playerState` holds player progress (inventory, coins, stats, flags,
  seen dialog). `ResetPlayerState()` replaces it with a fresh `new PlayerState(config)`.
- Cross-scene respawn already exists: `PlayerController.RespawnAtCheckpointNow` calls
  `G.Checkpoint.RequestRespawn()` then loads the checkpoint scene; the loaded
  `PlayerController.Start` consumes the pending respawn, teleports the hero to the bonfire
  spawn, and restores health. `StateRoot.Start` re-applies persistent + session state.

## "Saved game exists" detection
A saved resting point exists exactly when `G.Checkpoint.Current.HasValue`. This single
check drives whether the **Continue** button is shown.

## Design

### Main menu (`MainMenu.cs`)
- Add serialized references to the **Continue** button and the **New Game** button
  (the latter is the renamed "Start Game" button).
- On open (`OnEnable`, since the window GameObject is freshly activated each time it is
  shown), set the Continue button active only when `G.Checkpoint.Current.HasValue`.
  New Game is always active.
- **`OnContinueClick()`** — mirrors the existing cross-scene death-respawn path:
  ```csharp
  G.Checkpoint.RequestRespawn();
  var scene = G.Checkpoint.Current.Value.Scene.GetSceneName();
  G.Menu.CloseAll(() => G.SceneTravel.LoadScene(scene));
  ```
  The loaded scene's `PlayerController.Start` consumes the pending respawn (teleport to
  bonfire spawn + health restore), and `StateRoot.Start` re-applies world state. No
  session wipe — Continue resumes the world as it was, repositioned to the resting point,
  consistent with how death-respawn already behaves.
- **`OnNewGameClick()`** — if `G.Checkpoint.Current.HasValue`, open the confirmation
  dialog; otherwise reset-and-start immediately.
- **Reset-and-start** (private helper) — perform a full reset, then
  `G.Menu.CloseAll(() => G.SceneTravel.LoadScene(startScene.GetSceneName()))`. Running the
  reset unconditionally guarantees New Game always begins from the very beginning, even on
  first launch where state is already fresh (reset is idempotent there).

### Full reset
Clears all in-memory progress:
- `G.Game.ResetPlayerState()` — already exists; wipes inventory, coins, stats, flags,
  seen dialog by constructing a fresh `PlayerState`.
- **`CheckpointService.Reset()`** (new) — set `Current = null`, clear `discovered`,
  clear `HasPendingRespawn` / `IsBonfireRestTransitionActive`, and fire
  `OnCheckpointChanged(null)` so any live bonfire visuals update.
- **`SceneStateService.ResetAll()`** (new) — clear both `sessionStore` and
  `persistentStore` (existing `OnBonfireRest` only clears the session tier).

### Confirmation dialog (Option A — reusable window)
There is no generic confirm/yes-no dialog in the project (the `Dialog` folder is in-game
NPC dialog). Add a small reusable one following the per-window `MenuManager` convention:

- **`ConfirmDialog : AnimatedWindow`** (new script) with:
  - serialized message label (TMP) and Confirm / Cancel buttons,
  - `public void Configure(string message, Action onConfirm)` storing the callback and
    setting the label,
  - `OnConfirmClick()` → close self, invoke `onConfirm`; `OnCancelClick()` → close self.
- New prefab `ConfirmDialog.prefab`, duplicated from `PauseMenu.prefab` to reuse the
  Modal animator and `MenuButton` styling.
- Add `public MenuWindow ConfirmDialog;` to `MainConfig` (Menus header).
- MainMenu opens it via `G.Menu.OpenMenu(G.Config.ConfirmDialog)` and calls `Configure`
  with the warning message and the reset-and-start helper as the confirm callback.

## Files touched
| File | Change |
|------|--------|
| `Assets/Game/UI/MainMenu/MainMenu.cs` | rename Start→New Game handler, add Continue + New Game logic, button refresh, confirm wiring |
| `Assets/Game/Core/Services/CheckpointService.cs` | add `Reset()` |
| `Assets/Game/Core/Services/SceneState/SceneStateService.cs` | add `ResetAll()` |
| `Assets/Game/Configs/MainConfig.cs` | add `ConfirmDialog` field |
| `Assets/Game/UI/ConfirmDialog/ConfirmDialog.cs` (new) | reusable confirm window |
| `docs/system/` | add a short feature writeup when implemented |

## Manual Unity Editor steps
- `MainMenu.prefab`: add a **Continue** button, rename "Start Game" → "New Game", wire the
  buttons to `OnContinueClick` / `OnNewGameClick`, and assign the Continue/New Game button
  references on the `MainMenu` component.
- Create `ConfirmDialog.prefab` (duplicate `PauseMenu.prefab`, swap in the confirm layout
  with a message label + Confirm/Cancel buttons, attach `ConfirmDialog`), wire its buttons,
  and assign the message label + buttons.
- Assign `ConfirmDialog.prefab` to `MainConfig.ConfirmDialog` (`Resources/Configs/MainConfig`).

## Risks / notes
- `ConfirmDialog` and `MainConfig` changes touch serialized data — the new prefab and the
  new MainConfig reference must be wired in the editor or the dialog won't appear.
- Continue relies on `CheckpointService` / `SceneStateService` instances persisting since
  the game was played this run; correct for in-session use.
- No gameplay/movement/physics behavior changes.

## Out of scope
- Disk persistence / save-to-file (Continue across full app restarts).
