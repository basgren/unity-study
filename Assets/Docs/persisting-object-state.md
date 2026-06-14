# Persisting Object State

Objects in scenes can save and restore their state across scene transitions and bonfire rests using
the **StateRoot + saver component** pattern.

---

## Quick Start

1. Add a **`StateRoot`** component to the GameObject.
2. Add one or more **saver components** (see below) as siblings.
3. Set the **`tier`** on `StateRoot` in the Inspector.
4. Open **Tools → Scene State → Rebuild Scene Catalog** to register any newly added scenes.

That is all. State capture and restore happen automatically on every scene transition.

---

## Save Tiers

| Tier | When it clears | Typical use |
|------|---------------|-------------|
| **Session** | When the player rests at a bonfire | Moved barrels, weakened enemies |
| **Persistent** | Never (lasts the entire playthrough) | Opened doors, consumed Helms |

Set the tier per-instance in the `StateRoot` inspector. The same prefab can have different tiers
in different scenes with no code changes.

---

## Built-In Savers

| Component | Slot key | What it saves |
|-----------|----------|---------------|
| `TransformStateSaver` | `"transform"` | `transform.position` (+ optional rotation) |
| `ExistenceStateSaver` | `"exists"` | Whether the object has been permanently destroyed |
| `SwitchStateSaver` | `"switch"` | `Switch.isDisabled` (single-use switches, e.g. Helm) |
| `SwitchableStateSaver` | `"switchable"` | `SwitchableBase.IsActive` (e.g. StoneDoor open state) |
| `DamageableStateSaver` | `"damageable"` | `Damageable.currentHealth` |

---

## Wiring Guide for Existing Objects

### Helm (Persistent — used once, stays disabled)

Add to the Helm prefab instance:
- `StateRoot` → tier: **Persistent**
- `SwitchStateSaver`

### StoneDoor (Persistent — once opened, stays open)

Add to the StoneDoor prefab instance:
- `StateRoot` → tier: **Persistent**
- `SwitchableStateSaver`

### Destructible Barrel (Persistent or Session)

Add to the BarrelDestructable prefab instance:
- `StateRoot` → tier: **Persistent** (or **Session** if barrels should respawn on rest)
- `ExistenceStateSaver`

**Important:** In the Damageable `onDeath` UnityEvent, call
**`ExistenceStateSaver.MarkDestroyedAndDestroy`** instead of `GameObject.Destroy`. This pushes the
destruction into the service immediately so it is preserved even if the player quits before leaving
the scene.

### DraggableBarrel (Session — resets position on rest)

Add to the Barrel prefab instance:
- `StateRoot` → tier: **Session**
- `TransformStateSaver`

---

## Writing a Custom Saver

```csharp
using Game.Core.Services.SceneState;
using UnityEngine;

public sealed class MyStateSaver : MonoBehaviour, IStateSaver {
    public string Slot => "mySlot";   // unique within this StateRoot

    public void Capture(IStateWriter w) {
        w.SetBool("flag", myComponent.IsActive);
        w.SetFloat("value", myComponent.Amount);
    }

    public void Restore(IStateReader r) {
        if (r.TryGetBool("flag", out var flag)) {
            myComponent.IsActive = flag;
        }
        if (r.TryGetFloat("value", out var amount)) {
            myComponent.Amount = amount;
        }
    }
}
```

Add `MyStateSaver` as a sibling of `StateRoot` on the same GameObject. No registration is needed.

**Slot keys must be unique within one StateRoot** but can repeat across different GameObjects.

---

## Scene Catalog

The state system uses the scene's **asset GUID** (not its filename) as the key, so saved state
survives scene renames and moves.

The `SceneCatalog` ScriptableObject is the edit-time registry that maps scene paths to GUIDs.

- **Create:** `Assets → Create → Config → Scene Catalog`, then assign it to `MainConfig.SceneCatalog`.
- **Populate:** run **Tools → Scene State → Rebuild Scene Catalog** after adding or removing scenes
  from the build settings. The catalog is also rebuilt automatically before each build.

If a scene is not in the catalog, capture and restore are silently skipped for that scene and a
one-time warning is logged in the console.

---

## How State Flows

```
Scene A playing
│
├── Player activates a Helm
│   └── SwitchStateSaver pushes "disabled=true" immediately via G.SceneState.PushSlot
│
├── Player walks through a door → G.SceneTravel.LoadScene("SceneB")
│   ├── BeforeUnload fires → SceneStateService.CaptureScene(SceneA)
│   │   └── All StateRoots in SceneA have Capture() called (snapshot-on-leave)
│   └── Scene A unloads, Scene B loads
│
Scene B playing
│
└── Each StateRoot.Start() calls G.SceneState.RestoreInto(this)
    └── Each saver's Restore() is called with previously captured data
```

On **bonfire rest** and on **player death/respawn**, `SceneStateService.ClearSessionState()` clears
the Session store and the scene reloads. Session-tier objects return to their default scene state;
Persistent-tier objects are re-applied via `StateRoot.Start`.

---

## API Reference

```csharp
// Load a scene (fires BeforeUnload before unloading the old scene)
G.SceneTravel.LoadScene("SceneName");
G.SceneTravel.ReloadActiveScene();

// Skip state capture when the destination has no gameplay (e.g. main menu)
G.SceneTravel.LoadScene("MainMenu", new SceneLoadOptions { SkipStateCapture = true });

// Push a single slot immediately (useful in response to events, before scene unload)
G.SceneState.PushSlot(stateRoot, "mySlot", w => w.SetBool("key", value));

// Clear session state (called automatically by Bonfire.DoInteract and on player death/respawn)
G.SceneState.ClearSessionState();
```
