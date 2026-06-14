# Main Menu: Continue / New Game Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the single "Start Game" button with **Continue** (resume from the last bonfire resting point) and **New Game** (full in-memory reset, start from the beginning), where Continue is shown only when a saved resting point exists.

**Architecture:** Drive Continue visibility off `G.Checkpoint.Current.HasValue`. Continue reuses the existing cross-scene respawn path (`RequestRespawn` + load checkpoint scene). New Game performs a full in-memory reset (player state + checkpoint + scene state) then loads the start scene, behind a reusable confirmation window when a save exists. All state is in-memory only (no disk persistence).

**Tech Stack:** Unity 2021+ C#, NUnit edit-mode tests, existing `MenuManager` / `AnimatedWindow` UI framework, `G` global service locator.

**Spec:** `docs/superpowers/specs/2026-06-14-main-menu-continue-design.md`

---

## ⚠️ Commit policy for this repo

The user commits manually (preference: never auto-commit). **Do NOT run `git commit` or `git add` automatically.** Where this plan marks a "Checkpoint", stop and tell the user the logical unit is complete so *they* can review and commit. Treat each Checkpoint as a review boundary, not a commit action.

## ⚠️ Unity-specific notes

- **`.meta` files:** every new `.cs`/prefab gets a `.meta` from Unity. When creating new files, let Unity generate the `.meta` (or note it as a manual step). Never hand-author or delete `.meta` files.
- **No editing Unity YAML directly:** `MainConfig`, prefabs, and scenes are wired in the Editor by the user. This plan lists those as **Manual Unity Steps** — do not edit `.prefab`/`.asset`/ProjectSettings YAML by hand.
- **Compile verification:** This environment cannot launch Unity. Use the offline compile check (see Appendix A) as the automated gate after code edits. The authoritative behavior check is the user running Play mode (Task 6).

## File Structure

| File | Responsibility | Action |
|------|----------------|--------|
| `Assets/Game/Core/Services/CheckpointService.cs` | Checkpoint tracking; add full reset | Modify |
| `Assets/Game/Editor/Tests/Checkpoint/CheckpointServiceTests.cs` | Unit test for `Reset()` | Create |
| `Assets/Game/Core/Services/SceneState/SceneStateService.cs` | Scene state store; add full reset | Modify |
| `Assets/Game/UI/ConfirmDialog/ConfirmDialog.cs` | Reusable yes/no confirmation window | Create |
| `Assets/Game/Configs/MainConfig.cs` | Add `ConfirmDialog` prefab reference | Modify |
| `Assets/Game/UI/MainMenu/MainMenu.cs` | Continue / New Game logic + button refresh | Modify |
| `Assets/Game/UI/MainMenu/MainMenu.prefab` | Add Continue button, rename, wire handlers | Manual Unity |
| `Assets/Game/UI/ConfirmDialog/ConfirmDialog.prefab` | Confirm window prefab | Manual Unity |
| `docs/system/main-menu-continue.md` | Feature writeup | Create |

---

## Task 1: `CheckpointService.Reset()`

**Files:**
- Modify: `Assets/Game/Core/Services/CheckpointService.cs`
- Test: `Assets/Game/Editor/Tests/Checkpoint/CheckpointServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Assets/Game/Editor/Tests/Checkpoint/CheckpointServiceTests.cs`:

```csharp
using Game.Core.Services;
using Game.Features.Interactive.Bonfire;
using NUnit.Framework;
using UnityEngine;

namespace Game.Editor.Tests.Checkpoint {
    public class CheckpointServiceTests {
        private CheckpointService NewService() {
            var go = new GameObject("CheckpointService");
            return go.AddComponent<CheckpointService>();
        }

        private static CheckpointRef Ref(string localId) {
            // Default SceneReference is empty; GetSceneName() returns "" which is fine for these tests.
            return new CheckpointRef { LocalId = localId };
        }

        [Test]
        public void Reset_ClearsCurrentAndPendingRespawn() {
            var service = NewService();
            service.Activate(Ref("cp_a"));
            service.RequestRespawn();
            Assert.IsTrue(service.Current.HasValue);
            Assert.IsTrue(service.HasPendingRespawn);

            service.Reset();

            Assert.IsFalse(service.Current.HasValue);
            Assert.IsFalse(service.HasPendingRespawn);
            Assert.IsFalse(service.IsBonfireRestTransitionActive);

            Object.DestroyImmediate(service.gameObject);
        }

        [Test]
        public void Reset_ForgetsDiscoveredBonfires() {
            var service = NewService();
            service.Activate(Ref("cp_a"));
            Assert.AreEqual(BonfireState.Current, service.GetBonfireState("", "cp_a"));

            service.Reset();

            Assert.AreEqual(BonfireState.Undiscovered, service.GetBonfireState("", "cp_a"));

            Object.DestroyImmediate(service.gameObject);
        }

        [Test]
        public void Reset_FiresCheckpointChangedWithNull() {
            var service = NewService();
            service.Activate(Ref("cp_a"));

            CheckpointRef? lastValue = Ref("sentinel");
            var fired = false;
            service.OnCheckpointChanged += value => { fired = true; lastValue = value; };

            service.Reset();

            Assert.IsTrue(fired);
            Assert.IsFalse(lastValue.HasValue);

            Object.DestroyImmediate(service.gameObject);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run via Unity Test Runner (EditMode) `Game.Editor.Tests.Checkpoint.CheckpointServiceTests`, or the offline compile check (Appendix A).
Expected: compile error / FAIL — `CheckpointService` has no `Reset` method.

- [ ] **Step 3: Add `Reset()` to `CheckpointService`**

In `Assets/Game/Core/Services/CheckpointService.cs`, add this method (place it after `Activate`):

```csharp
/// <summary>
/// Fully clears all checkpoint progress. Used when starting a new game.
/// Forgets the active checkpoint and every discovered bonfire and cancels any pending respawn.
/// </summary>
public void Reset() {
    Current = null;
    discovered.Clear();
    HasPendingRespawn = false;
    IsBonfireRestTransitionActive = false;
    OnCheckpointChanged?.Invoke(Current);
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run the EditMode tests (or Appendix A compile check + Test Runner).
Expected: all three `CheckpointServiceTests` PASS.

- [ ] **Step 5: Checkpoint** — tell the user Task 1 is complete for review/commit. Do not commit.

---

## Task 2: `SceneStateService.ResetAll()`

`sessionStore` / `persistentStore` are private with no read accessor, and populating them requires `StateRoot` objects in a loaded scene — an integration scenario, not a unit test. This method is verified by compile (Appendix A) and Play-mode (Task 6). No unit test is added, consistent with the project only unit-testing pure-logic classes.

**Files:**
- Modify: `Assets/Game/Core/Services/SceneState/SceneStateService.cs`

- [ ] **Step 1: Add `ResetAll()` next to `OnBonfireRest`**

In `Assets/Game/Core/Services/SceneState/SceneStateService.cs`, directly below the existing `OnBonfireRest` method:

```csharp
/// <summary>
/// Clears ALL captured scene state, both session and persistent tiers.
/// Used when starting a new game so no world progress (opened doors, destroyed
/// objects, weakened enemies, etc.) carries over from the previous run.
/// </summary>
public void ResetAll() {
    sessionStore.Clear();
    persistentStore.Clear();
}
```

- [ ] **Step 2: Verify it compiles**

Run the offline compile check (Appendix A) over `Assembly-CSharp`.
Expected: no new errors mentioning `SceneStateService.cs`.

- [ ] **Step 3: Checkpoint** — tell the user Task 2 is complete for review/commit.

---

## Task 3: `ConfirmDialog` window script + `MainConfig` field

**Files:**
- Create: `Assets/Game/UI/ConfirmDialog/ConfirmDialog.cs`
- Modify: `Assets/Game/Configs/MainConfig.cs`

- [ ] **Step 1: Create the `ConfirmDialog` script**

Create `Assets/Game/UI/ConfirmDialog/ConfirmDialog.cs`:

```csharp
using System;
using Game.Core.Bootstrap;
using Game.Core.UI;
using TMPro;
using UnityEngine;

namespace Game.UI.ConfirmDialog {
    /// <summary>
    /// Reusable yes/no confirmation window. Configure it with a message and an
    /// onConfirm callback before/just after opening, then it closes itself when the
    /// player chooses. Confirm invokes the callback; Cancel just dismisses.
    /// </summary>
    public class ConfirmDialog : AnimatedWindow {
        [SerializeField]
        private TMP_Text messageLabel;

        private Action onConfirm;

        /// <summary>
        /// Sets the prompt text and the action to run if the player confirms.
        /// </summary>
        public void Configure(string message, Action onConfirm) {
            this.onConfirm = onConfirm;
            if (messageLabel != null) {
                messageLabel.text = message;
            }
        }

        public void OnConfirmClick() {
            var callback = onConfirm;
            onConfirm = null;
            G.Menu.CloseTopWindow();
            callback?.Invoke();
        }

        public void OnCancelClick() {
            onConfirm = null;
            G.Menu.CloseTopWindow();
        }
    }
}
```

> Note: confirm the project uses `TMPro` for menu text (the existing menu prefabs use TextMeshPro). If a menu uses `UnityEngine.UI.Text` instead, swap `TMP_Text` for `Text` and the `using`. Check `Assets/Game/UI/PauseMenu/PauseMenu.prefab` text component before finalizing the prefab in Task 5.

- [ ] **Step 2: Add the `ConfirmDialog` reference to `MainConfig`**

In `Assets/Game/Configs/MainConfig.cs`, under the `[Header("Menus")]` block (after `PauseMenu`), add:

```csharp
        public MenuWindow ConfirmDialog;
```

The block should read:

```csharp
        [Header("Menus")]
        public MenuWindow MainMenu;
        public MenuWindow OptionsMenu;
        public MenuWindow PauseMenu;
        public MenuWindow ConfirmDialog;
```

- [ ] **Step 3: Verify it compiles**

Run the offline compile check (Appendix A) over `Assembly-CSharp`.
Expected: no new errors mentioning `ConfirmDialog.cs` or `MainConfig.cs`.
(`G.Menu`, `AnimatedWindow`, `TMP_Text` all already referenced by the runtime assembly.)

- [ ] **Step 4: Checkpoint** — tell the user Task 3 is complete for review/commit.

---

## Task 4: `MainMenu` Continue / New Game logic

**Files:**
- Modify: `Assets/Game/UI/MainMenu/MainMenu.cs`

- [ ] **Step 1: Replace `MainMenu.cs` contents**

Replace the body of `Assets/Game/UI/MainMenu/MainMenu.cs` with:

```csharp
using Game.Core.Bootstrap;
using Game.Core.Services.Scene;
using Game.Core.UI;
using Game.UI.ConfirmDialog;
using UnityEngine;

namespace Game.UI.MainMenu {
    public class MainMenu : AnimatedWindow {
        [SerializeField]
        private SceneReference startScene;

        [SerializeField]
        [Tooltip("Shown only when a saved resting point exists. Resumes from the last checkpoint.")]
        private GameObject continueButton;

        [SerializeField]
        [Tooltip("Confirmation message shown before New Game wipes existing progress.")]
        private string newGameConfirmMessage = "Start a new game? This will erase your current progress.";

        private void OnEnable() {
            RefreshContinueButton();
        }

        private void RefreshContinueButton() {
            if (continueButton != null) {
                continueButton.SetActive(HasSavedGame());
            }
        }

        private static bool HasSavedGame() {
            return G.Checkpoint != null && G.Checkpoint.Current.HasValue;
        }

        public void OnContinueClick() {
            if (!HasSavedGame()) {
                return;
            }

            // Mirror the cross-scene death-respawn path: flag a pending respawn, then load the
            // checkpoint's scene. The loaded PlayerController.Start teleports the hero to the
            // bonfire spawn and restores health; StateRoot.Start re-applies world state.
            var checkpointScene = G.Checkpoint.Current.Value.Scene.GetSceneName();
            G.Checkpoint.RequestRespawn();
            G.Menu.CloseAll(() => G.SceneTravel.LoadScene(checkpointScene));
        }

        public void OnNewGameClick() {
            if (HasSavedGame()) {
                var dialog = G.Menu.OpenMenu(G.Config.ConfirmDialog) as ConfirmDialog;
                if (dialog != null) {
                    dialog.Configure(newGameConfirmMessage, ResetAndStart);
                    return;
                }

                // Fallback: if the confirm dialog is not wired, start anyway rather than dead-end.
                Debug.LogWarning("MainMenu: ConfirmDialog is not assigned in MainConfig; starting new game without confirmation.");
            }

            ResetAndStart();
        }

        private void ResetAndStart() {
            // Full in-memory reset so the new game starts from the very beginning.
            G.Game.ResetPlayerState();
            G.Checkpoint.Reset();
            G.SceneState.ResetAll();

            G.Menu.CloseAll(() => G.SceneTravel.LoadScene(startScene.GetSceneName()));
        }

        public void OnOptionsClick() {
            G.Menu.OpenOptionsMenu();
        }

        public void OnExitClick() {
            G.Menu.CloseAll();
            Application.Quit();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
```

Notes for the implementer:
- The old `OnStartGameClick()` is replaced by `OnNewGameClick()`. The prefab button currently wired to `OnStartGameClick` MUST be re-wired to `OnNewGameClick` (Manual Unity Step, Task 5) or the button stops working.
- Verify `G.Game`, `G.Checkpoint`, `G.SceneState`, `G.Config` exist on the `G` static (they are existing services). If any accessor name differs, adjust.

- [ ] **Step 2: Verify it compiles**

Run the offline compile check (Appendix A) over `Assembly-CSharp`.
Expected: no new errors mentioning `MainMenu.cs`. Confirm `G.Game.ResetPlayerState`, `G.Checkpoint.Reset`, `G.SceneState.ResetAll`, `G.Config.ConfirmDialog` all resolve.

- [ ] **Step 3: Checkpoint** — tell the user Task 4 is complete for review/commit.

---

## Task 5: Manual Unity wiring (user performs in the Editor)

These steps require the Unity Editor and cannot be scripted here. Present them to the user as a checklist.

- [ ] **Step 1: ConfirmDialog prefab**
  1. Duplicate `Assets/Game/UI/PauseMenu/PauseMenu.prefab` → move to `Assets/Game/UI/ConfirmDialog/ConfirmDialog.prefab` (keep its `.meta` via `git mv` if renaming on disk; preferred path is duplicating inside the Editor so Unity assigns a fresh GUID).
  2. Remove `PauseMenu` component; add the new `ConfirmDialog` component.
  3. Lay out a message label (TMP_Text) + two `MenuButton`s ("Yes"/"Confirm" and "No"/"Cancel").
  4. Assign `messageLabel` on the `ConfirmDialog` component.
  5. Wire the Confirm button's `onClick` → `ConfirmDialog.OnConfirmClick`; the Cancel button's `onClick` → `ConfirmDialog.OnCancelClick`.
  6. Set `MenuWindow` flags: `closeOnCancel = true` (Cancel via Escape), `pausesGame` per preference, `dimsBackground = true`. Set `FirstSelected` to the Cancel button (safe default).

- [ ] **Step 2: MainConfig**
  1. Select `Assets/Resources/Configs/MainConfig`.
  2. Assign the new `ConfirmDialog.prefab` to the **Confirm Dialog** field under Menus.

- [ ] **Step 3: MainMenu prefab**
  1. Open `Assets/Game/UI/MainMenu/MainMenu.prefab`.
  2. Rename the existing "Start Game" button label → **New Game**. Re-wire its `onClick` from `OnStartGameClick` → `MainMenu.OnNewGameClick`.
  3. Add a new **Continue** button (duplicate the New Game button for consistent styling), placed above New Game. Wire its `onClick` → `MainMenu.OnContinueClick`.
  4. On the `MainMenu` component, assign the **Continue Button** field to the Continue button GameObject.
  5. Set the window's `FirstSelected` to the **New Game** button (always active, so focus never lands on a hidden Continue).
  6. (Optional) adjust the vertical layout so hiding Continue collapses the gap cleanly.

- [ ] **Step 4: Checkpoint** — user confirms wiring is done.

---

## Task 6: Verification (user, in Play mode)

- [ ] **Step 1: Fresh launch**
  - Enter Play mode from the MainMenu scene with no prior game.
  - Expected: only **New Game** is visible (no Continue). Options/Exit unchanged.

- [ ] **Step 2: New Game from fresh state**
  - Click New Game.
  - Expected: no confirmation prompt (no save exists); start scene loads; player at full health, empty inventory, 0 coins, base stats.

- [ ] **Step 3: Create a resting point**
  - Play to a bonfire and rest at it.

- [ ] **Step 4: Exit to menu, Continue**
  - Open pause menu → Exit to Menu.
  - Expected: **Continue** and **New Game** both visible.
  - Click Continue.
  - Expected: the checkpoint's scene loads; hero spawns at the bonfire spawn point at full health; persistent world state (e.g. opened doors) is preserved.

- [ ] **Step 5: New Game confirmation + reset**
  - Exit to menu again → click New Game.
  - Expected: confirmation dialog appears. Click Cancel → returns to menu, nothing changes. Click New Game → confirm → start scene loads from the very beginning; the previously rested bonfire is no longer the active checkpoint; inventory/coins/stats are reset.

- [ ] **Step 6: Bonfire visuals after reset**
  - After a New Game reset, revisit a previously discovered bonfire.
  - Expected: it renders as Undiscovered (proves `CheckpointService.Reset` cleared `discovered`).

---

## Task 7: Documentation

**Files:**
- Create: `docs/system/main-menu-continue.md`

- [ ] **Step 1: Write the feature doc**

Create `docs/system/main-menu-continue.md` summarizing: the Continue/New Game behavior, the in-session-only persistence model, the `Current.HasValue` detection, the reset surface (`GameManager.ResetPlayerState` + `CheckpointService.Reset` + `SceneStateService.ResetAll`), the `ConfirmDialog` window, and the manual wiring (MainMenu prefab buttons, ConfirmDialog prefab, MainConfig field). Cross-reference `docs/system/state-saving.md`.

- [ ] **Step 2: Checkpoint** — tell the user the feature is complete for final review/commit.

---

## Appendix A: Offline compile check

This environment cannot launch Unity. To verify C# compiles without the Editor, reuse Unity's Bee response file with the bundled Roslyn compiler (see memory `reference_offline_unity_compile_check`):

1. Find the response file: `Library/Bee/artifacts/<hash>.dag/Assembly-CSharp.rsp` (runtime code: `MainMenu`, `CheckpointService`, `SceneStateService`, `ConfirmDialog`, `MainConfig`) and `Assembly-CSharp-Editor.rsp` (the test). `<hash>` changes per build — glob for it.
2. Clone the `.rsp`, redirecting `-out:`/`-refout:` to a throwaway path (e.g. `Temp/obcompile/out.dll`) so real artifacts aren't clobbered.
3. Compile with Unity's bundled dotnet + csc:
   `<UnityHub>/Editor/<ver>/Editor/Data/NetCoreRuntime/dotnet.exe exec <...>/DotNetSdkRoslyn/csc.dll -nostdlib -noconfig "@<clone>.rsp"`

Gotchas:
- In git-bash, pass dash-form flags (`-nostdlib -noconfig`) or set `MSYS2_ARG_CONV_EXCL="*"` to avoid path-mangling.
- `-noconfig` must be on the command line, not inside the `.rsp`.
- The whole assembly compiles, so pre-existing unrelated errors may surface — filter output by the filenames you changed.

If the offline check is impractical, the fallback is to let the user's open Unity Editor compile and run the EditMode tests via Test Runner.

---

## Self-Review (completed by plan author)

- **Spec coverage:** detection (`Current.HasValue`) → Task 4; Continue → Task 4; New Game + rename → Task 4; full reset (`ResetPlayerState`/`Reset`/`ResetAll`) → Tasks 1, 2, 4; ConfirmDialog + MainConfig → Task 3; prefab/config wiring → Task 5; docs → Task 7. All spec sections covered.
- **Placeholder scan:** no TBD/TODO; all code steps contain full code.
- **Type consistency:** `Reset()`, `ResetAll()`, `Configure(string, Action)`, `OnConfirmClick()`/`OnCancelClick()`, `continueButton`, `ResetAndStart()`, `HasSavedGame()`, `ConfirmDialog` MainConfig field used consistently across tasks.
- **Known assumptions to verify during execution:** `G.Game`/`G.Checkpoint`/`G.SceneState`/`G.Config` accessor names; menu text uses `TMPro`; `OpenMenu` returns the instantiated window (it does, per `MenuManager.OpenMenu<T>`).
```