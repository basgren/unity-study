# Interaction Gates & Key Locks

## Goal
Let a world interactable (Door, Switch, …) be conditionally controlled at the moment the player
activates it, while still showing its normal hover/hint. The first concrete use is a **key-locked
door**:

- No key → the door does not open; a "locked" sound plays.
- Has key → the key is consumed, a "key turning" sound plays, and the door opens after a short delay.
- Once unlocked it stays unlocked (opens normally, no key/sound), and that state **persists for the
  rest of the game** so the player isn't soft-locked after the single-use key is gone.

## Why a seam in `InteractableBase`
A door's open sequence (control lock, open animation, open sound, and `TravelToTarget` via an
animation event) is driven entirely by the prefab's `onInteract` **UnityEvent**.
`InteractableBase.Interact()` fires that event before `DoInteract()`. So a gate that must suppress,
reject-with-feedback, or *delay-then-run* the open has to sit **before** `onInteract` fires. Hence the
gate lives in the base; the lock logic lives in a small composable component.

```mermaid
sequenceDiagram
    participant R as InteractionResolver
    participant I as InteractableBase
    participant K as KeyLock (IInteractionGate)
    participant Inv as InventoryModel
    R->>I: Interact()
    I->>K: OnInteractRequested()
    alt unlocked / no key required
        K-->>I: Allow
        I->>I: Activate()  (onInteract + DoInteract)
    else locked, no key
        K->>K: play lockedSound
        K-->>I: Reject
        Note over I: open never runs
    else locked, has key
        K->>Inv: Remove(key, 1)
        K->>K: IsUnlocked = true; play unlockSound
        K-->>I: Deferred
        Note over K: after openDelayAfterUnlock...
        K->>I: Activate()  (open now)
    end
```

## Pieces
- `InteractionGateResult` — `Allow` / `Reject` / `Deferred`.
- `IInteractionGate.OnInteractRequested()` — called once per activation; may play feedback or start
  async work. Returns the result.
- `InteractableBase`
  - caches `GetComponents<IInteractionGate>()` in `Awake`;
  - `Interact()` consults gates and only proceeds on `Allow`;
  - **`Activate()`** runs the real interaction (`onInteract` + `DoInteract`) bypassing gates — called by
    `Interact()` on `Allow`, or by a deferring gate when its sequence finishes.
  - Gateless interactables are unaffected (empty array → always `Allow`).
- `KeyLock` — the gate: requires `requiredKey` (`ItemId`); consumes it and runs unlock SFX + delay on
  success, plays `lockedSound` on failure. Holds the persisted `IsUnlocked` flag. An unset key never
  locks (avoids accidental soft-lock). Re-presses during the unlock delay are ignored.
- `KeyLockStateSaver` — `StateSaverBase` that persists `KeyLock.IsUnlocked` (slot `"keyLock"`,
  snapshot-on-leave), mirroring `SwitchableStateSaver`.

`CanInteract()` is intentionally left untouched, so a locked door stays a normal interaction target:
the player still sees the hint, can press, and hears feedback.

## Why persistence is required, not optional
`G.Game.playerState` (and its `InventoryModel`) is `DontDestroyOnLoad`, so inventory persists at the
game level — a consumed key is gone for good. Without persisting the unlocked flag, returning to the
scene would re-lock a door whose key no longer exists. So the lock's unlocked state **must** persist
(`Persistent` tier). See `state-saving.md`.

## Usage (Unity)
On the **scene instance** of the door to lock (not the shared prefab, unless every door of that prefab
is locked), all on the same GameObject as `Door`:
1. Add `KeyLock`; set `Required Key` (e.g. `RustyKey`), `Locked Sound`, `Unlock Sound`, and tune
   `Open Delay After Unlock` (seconds).
2. Add `StateRoot`; set `Tier = Persistent`.
3. Add `KeyLockStateSaver`.
4. **Save the scene** (assigns the `StateRoot` Save ID) and run
   **Tools → Scene State → Rebuild Scene Catalog** if the scene is new to the catalog.

## Not included / follow-ups
- Controls are not locked during the brief unlock delay (the door's `BeginTravelTransition` only runs
  when the open finally fires). Add an early control-lock hook if that matters.
- Distinct "locked" hint text vs the normal "Open" verb.
