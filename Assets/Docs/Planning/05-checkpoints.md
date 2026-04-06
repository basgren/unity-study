# 05 - Checkpoint System

## Goal

Let the player activate bonfires as checkpoints. On death, respawn at the last activated bonfire instead of restarting the scene. Checkpoints work across scenes.

## Current State

**Bonfire** (`Assets/Game/Features/Interactive/Bonfire/Bonfire.cs`):
- Already has three states: `Undiscovered`, `Discovered`, `Current`.
- Extends `InteractableBase`, plays sprite animations via `MultiStateSpriteAnimator`.
- On interact: sets own state to `Current`, but nothing is persisted — state resets on scene load.

**Player death** (`PlayerController.cs`):
- Health reaches 0 → `ShowHitAndRestartScene()` → waits 2.5 s → `SceneManager.LoadScene(currentScene)`.
- `RespawnOnContact` damager → `ShowHitAndRespawnAtSafePoint()` → teleports to `SafePointTracker.LastSafePosition`.
- Scene reload does not reset `PlayerState` (services survive via `DontDestroyOnLoad`), but `currentHealth` is already 0 at that point — the new `PlayerController` reads it via `InitFromState`. Health restoration on respawn needs to be added.

**PlayerState** (`PlayerState.cs`):
- In-memory state on `GameManager` (persists across scene loads).
- Has a `flags` list (set of strings) — usable for tracking discovered bonfires.

**No save-to-disk system exists.** All state is in-memory only.

## Design

### Checkpoint Data

```csharp
public struct CheckpointData {
    public string CheckpointId;
    public string SceneName;
    public Vector2 SpawnPosition;
}
```

Stored in a new `CheckpointService`. No disk persistence for now — data lives in memory and survives scene loads because services are under `DontDestroyOnLoad`.

### CheckpointService

New service registered in `G` / `GInit`, following the existing pattern.

```
Assets/Game/Core/Services/CheckpointService.cs
```

```csharp
public class CheckpointService : MonoBehaviour {
    public CheckpointData? Current { get; private set; }

    public event Action<CheckpointData?> OnCheckpointChanged;

    // Set of all checkpoint IDs the player has visited.
    private readonly HashSet<string> discovered = new();

    // When true, the next scene load should respawn the player at Current.
    public bool HasPendingRespawn { get; private set; }

    public void Activate(string checkpointId, string sceneName, Vector2 spawnPosition) {
        discovered.Add(checkpointId);
        Current = new CheckpointData {
            CheckpointId = checkpointId,
            SceneName = sceneName,
            SpawnPosition = spawnPosition,
        };
        HasPendingRespawn = false;
        OnCheckpointChanged?.Invoke(Current);
    }

    public BonfireState GetBonfireState(string checkpointId) {
        if (Current.HasValue && Current.Value.CheckpointId == checkpointId) {
            return BonfireState.Current;
        }
        if (discovered.Contains(checkpointId)) {
            return BonfireState.Discovered;
        }
        return BonfireState.Undiscovered;
    }

    /// <summary>
    /// Marks that the player should respawn at the current checkpoint
    /// after the next scene load.
    /// </summary>
    public void RequestRespawn() {
        HasPendingRespawn = true;
    }

    /// <summary>
    /// Consumes the pending respawn flag and returns the spawn position.
    /// </summary>
    public Vector2 ConsumePendingRespawn() {
        HasPendingRespawn = false;
        return Current.Value.SpawnPosition;
    }
}
```

Registration in `G.cs`:

```csharp
public static CheckpointService Checkpoint { get; internal set; }
```

Registration in `GInit.cs` (after `G.Hero`, before other services — order is not critical):

```csharp
G.Checkpoint = GetOrCreate<CheckpointService>("CheckpointService");
```

No `[SerializeField]` on the service — it is created dynamically via `GInit`, so serialized fields would be unset. This follows the existing service configuration rules.

### Bonfire Changes

The `Bonfire` component gets a checkpoint ID and a spawn point:

```csharp
[SerializeField] private string checkpointId;
[SerializeField] private Transform spawnPoint;
```

- `checkpointId` — unique string, manually assigned per instance in the Inspector. Convention: `"{scene}_{name}"`, e.g. `"island_beach"`, `"ship_deck"`. Enforced only by convention, not by code.
- `spawnPoint` — child Transform marking where the player appears. If null, falls back to `transform.position + small offset` so the player doesn't overlap the bonfire collider.

#### Bonfire.Start

```csharp
private void Start() {
    var state = G.Checkpoint.GetBonfireState(checkpointId);
    SetState(state);
    G.Checkpoint.OnCheckpointChanged += OnCheckpointChanged;
}

private void OnDestroy() {
    G.Checkpoint.OnCheckpointChanged -= OnCheckpointChanged;
}
```

#### Bonfire.DoInteract

```csharp
protected override void DoInteract() {
    if (bonfireState == BonfireState.Current) {
        return; // Already active, do nothing (or open bonfire menu later).
    }

    var sceneName = SceneManager.GetActiveScene().name;
    var position = spawnPoint != null ? (Vector2)spawnPoint.position : (Vector2)transform.position;
    G.Checkpoint.Activate(checkpointId, sceneName, position);
}
```

#### Bonfire.OnCheckpointChanged

When another bonfire is activated, this one updates its visual:

```csharp
private void OnCheckpointChanged(CheckpointData? data) {
    var newState = G.Checkpoint.GetBonfireState(checkpointId);
    SetState(newState);
}
```

This handles the case where two bonfires are in the same scene — when one becomes `Current`, the other becomes `Discovered`.

### Death and Respawn Changes

`PlayerController` currently owns the death/respawn logic. There is a TODO in the code suggesting this should move to `GameManager`. For now, the smallest safe change is to modify `PlayerController` in place.

#### On Death (health reaches 0)

Replace `ShowHitAndRestartScene` behavior:

```csharp
// In OnAfterHit, where damageable.IsDead is true:
if (G.Checkpoint.Current.HasValue) {
    ShowHitAndRespawnAtCheckpoint();
} else {
    ShowHitAndRestartScene(); // Fallback: existing behavior.
}
```

New method:

```csharp
private void ShowHitAndRespawnAtCheckpoint() {
    Actions.Disable();
    isDead = true;
    isDiedThisFrame = true;
    damageable.IgnoreDamage = true;
    StartCoroutine(WaitAndRespawnAtCheckpoint(WaitBeforeRestart));
}

private IEnumerator WaitAndRespawnAtCheckpoint(float seconds) {
    yield return new WaitForSeconds(seconds);

    var checkpoint = G.Checkpoint.Current.Value;
    var currentScene = SceneManager.GetActiveScene().name;

    if (checkpoint.SceneName == currentScene) {
        // Same scene: teleport.
        RespawnAtPosition(checkpoint.SpawnPosition);
    } else {
        // Different scene: load scene, then teleport on arrival.
        G.Checkpoint.RequestRespawn();
        SceneManager.LoadScene(checkpoint.SceneName);
    }
}

private void RespawnAtPosition(Vector2 position) {
    isDead = false;
    transform.position = position;
    RestoreHealthAfterRespawn();
    damageable.IgnoreDamage = false;
    Actions.Enable();
}
```

#### Health Restoration

On respawn, health should be restored. Full heal is the simplest option:

```csharp
private void RestoreHealthAfterRespawn() {
    var maxHealth = state.GetMaxHealth();
    state.currentHealth = maxHealth;
    damageable.SetHealth(maxHealth);
}
```

This also fixes the existing issue where scene-reload respawn preserves 0 health in `PlayerState`.

#### Cross-Scene Respawn

When a scene loads and a new `PlayerController` is created, it must check for a pending checkpoint respawn:

```csharp
// In PlayerController, at the end of Awake or in Start:
private void Start() {
    if (G.Checkpoint.HasPendingRespawn) {
        var spawnPos = G.Checkpoint.ConsumePendingRespawn();
        transform.position = spawnPos;
        RestoreHealthAfterRespawn();
    }
}
```

### Checkpoint ID Uniqueness

IDs are strings assigned manually in the Inspector. Validation approach:

- **Editor-time**: Add a simple `OnValidate` warning in `Bonfire` that logs if `checkpointId` is empty.
- **Optional future enhancement**: Editor script that scans all scenes/prefabs for duplicate IDs. Not required for initial implementation.

The convention `"{scene}_{name}"` naturally avoids collisions across scenes. Within a scene, names like `"island_beach"` and `"island_cave"` are distinct enough.

### What About RespawnOnContact Damagers?

The `RespawnOnContact` path (hazards that don't kill, just teleport to safe point) remains unchanged. Checkpoints only affect full-death respawn. This keeps the two systems independent — safe points for minor hazards, checkpoints for death.

## Summary of File Changes

**Modified files:**

| File | Change |
|---|---|
| `Assets/Game/Core/Bootstrap/G.cs` | Add `Checkpoint` property |
| `Assets/Game/Core/Bootstrap/GInit.cs` | Create `CheckpointService` |
| `Assets/Game/Features/Interactive/Bonfire/Bonfire.cs` | Add checkpoint ID, spawn point, service integration |
| `Assets/Game/Features/Characters/Hero/PlayerController.cs` | Checkpoint respawn on death, cross-scene respawn in Start, health restoration |

**New files:**

| File | Purpose |
|---|---|
| `Assets/Game/Core/Services/CheckpointService.cs` | Service: tracks current checkpoint, discovered set, pending respawn |

## Unity Editor Steps

1. Open the Bonfire prefab. Add `checkpointId` string and `spawnPoint` Transform reference in Inspector.
2. For each bonfire instance in scenes, assign a unique `checkpointId` and optionally create a child empty GameObject as the spawn point.
3. No changes to `MainConfig` — `CheckpointService` has no configuration that needs to come from a ScriptableObject.

## Risks

**Cross-scene respawn timing.** The new `PlayerController` must read `HasPendingRespawn` before any other system moves the player or resets state. Using `Start()` (which runs after all `Awake()` calls) should be safe because services initialize in `Awake` via `AppBootstrap`, and the player is a scene object whose `Start` runs after.

**Duplicate checkpoint IDs.** No compile-time enforcement. If two bonfires share an ID, `GetBonfireState` will return correct state for the current one but both will respond to the same activation. Mitigation: naming convention + editor warning on empty ID.

**Health on scene restart without checkpoint.** The existing `ShowHitAndRestartScene` does not restore health in `PlayerState`. After the scene reloads, the new player reads `currentHealth = 0`. This is a pre-existing issue. The checkpoint implementation should also add `RestoreHealthAfterRespawn()` to the `WaitAndRestart` coroutine, or call `G.Game.ResetPlayerState()` before reload.

**No disk persistence.** Quitting the game loses all checkpoint progress. This is acceptable for now — a save system is a separate feature. When a save system is added, `CheckpointService` state (current checkpoint + discovered set) should be included in the save data.

## Future Extensions (not in scope now)

- Bonfire menu on interact (rest, fast travel to discovered bonfires).
- Disk save/load of checkpoint state.
- Visual/audio feedback on checkpoint activation (particles, sound, camera effect).
- Editor tool to validate checkpoint ID uniqueness across all scenes.