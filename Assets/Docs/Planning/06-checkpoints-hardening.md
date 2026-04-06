# 06 - Checkpoint ID Hardening

## Goal

Make checkpoint references reliable enough for future save/load persistence:
1. Guarantee uniqueness within each scene (per-scene, same as doors).
2. Use a composite key (scene reference + local ID) so the scene is always known from the reference itself — no lookup needed.
3. Make ID assignment simple with immediate feedback on collisions.
4. A checkpoint reference in a save file must be stable: removing a bonfire and placing a new one with the same local ID in the same scene should restore correctly.

## Key Decision: Composite Key

A checkpoint is identified by a **composite key**: `SceneReference` (GUID-based) + `string localId` (unique within the scene).

This means:
- **Uniqueness is per-scene only** (same scope as doors) — simpler validation, no cross-scene scanning.
- **Scene resolution is trivial** — the scene reference is part of the key itself, no lookup table needed.
- **Stability** — the GUID survives scene renames; the local ID is a stable string within the scene. If a bonfire is deleted and re-created with the same local ID in the same scene, the save file still matches.
- **No centralized registry or generated lookup asset required.**

## Current State

**Bonfire** (`Assets/Game/Features/Interactive/Bonfire/Bonfire.cs`):
- `checkpointId` is a plain `[SerializeField] string`, typed manually in the Inspector.
- Only validation: `OnValidate` logs a warning if the field is empty.
- No uniqueness check, no auto-generation, no editor tooling.

**CheckpointService** (`Assets/Game/Core/Services/CheckpointService.cs`):
- `CheckpointData` stores `string SceneName` (plain scene name, not GUID-based).
- Scene name is captured at activation time from `SceneManager.GetActiveScene().name`.
- No disk persistence yet. All state is in-memory under DontDestroyOnLoad.

**Door system** (reference implementation in `Assets/Game/Features/Doors/`):
- Doors use random alphanumeric IDs auto-generated in `OnValidate` (e.g. `Door_a3x8k`).
- Uniqueness enforced **per-scene**.
- Rich editor tooling: custom inspector, rename window with cross-project reference updates, per-scene door cache for dropdown pickers, play-mode validator, build validator, scene-reference repair on scene rename.
- `SceneReference` struct stores scene GUID (stable across renames) + cached path.

## Design

### 1. CheckpointRef: The Composite Key

```csharp
// Assets/Game/Core/Services/CheckpointRef.cs
[System.Serializable]
public struct CheckpointRef {
    public SceneReference Scene;
    public string LocalId;

    public bool IsEmpty => Scene.IsEmpty() || string.IsNullOrWhiteSpace(LocalId);
}
```

This is the stable reference that gets stored in save files. `SceneReference.SceneGuid` is the primary scene identifier (survives renames); `SceneReference.ScenePath` is a cached convenience; `LocalId` is unique within the scene.

**Important:** The `Scene` part of a `CheckpointRef` is never manually assigned. It is always derived from the Bonfire's own scene — either at runtime in `DoInteract` (from `gameObject.scene`) or at save time. There is no inspector field for it on Bonfire, and no way to point a checkpoint at a different scene. The Bonfire only exposes `checkpointId` (the local part); the scene binding is implicit and automatic.

### 2. CheckpointData Refactor

```csharp
// Assets/Game/Core/Services/CheckpointService.cs
public struct CheckpointData {
    public CheckpointRef Ref;
}
```

- `SceneName` is removed. Use `Ref.Scene.GetSceneName()` instead.
- **`SpawnPosition` is removed.** The spawn position is not stored — it is read from the live Bonfire object at respawn time via `Bonfire.GetSpawnPosition()`. This way, if a bonfire is moved between game updates, the player always spawns at its current position, and save files never contain stale coordinates.

### 3. Bonfire Changes

#### Public Spawn Position

Bonfire exposes its spawn position so the respawn flow can query it at runtime:

```csharp
public Vector2 GetSpawnPosition() {
    return spawnPoint != null ? (Vector2)spawnPoint.position : (Vector2)transform.position;
}
```

#### Activation

`Bonfire.DoInteract` constructs a `CheckpointRef` from the current scene. No position is passed — it will be read from the Bonfire at respawn time:

```csharp
protected override void DoInteract() {
    if (bonfireState == BonfireState.Current) {
        return;
    }

    var checkpointRef = new CheckpointRef {
        Scene = SceneReference.FromScene(gameObject.scene),
        LocalId = checkpointId,
    };
    G.Checkpoint.Activate(checkpointRef);
}
```

#### State Query

`GetBonfireState` changes to match on `LocalId` (the Bonfire only needs to compare its own local ID — the scene is implied because the Bonfire is in the same scene as the checkpoint it represents):

```csharp
// CheckpointService
public BonfireState GetBonfireState(string localId) {
    if (Current.HasValue && Current.Value.Ref.LocalId == localId) {
        return BonfireState.Current;
    }
    if (IsDiscovered(localId)) {
        return BonfireState.Discovered;
    }
    return BonfireState.Undiscovered;
}
```

Note: `discovered` set stores `CheckpointRef` or just local IDs. Since uniqueness is per-scene, storing just the local ID is ambiguous if the player visits bonfires with the same local ID in different scenes. Two options:

- **Store full `CheckpointRef`** in the discovered set — unambiguous, but needs equality comparison on the struct.
- **Store `"{sceneGuid}:{localId}"` string** — simple, unambiguous.

Recommended: store the composite string `"{sceneGuid}:{localId}"` for simplicity.

### 3a. BonfireUtils: Finding Bonfires by ID

Similar to `DoorUtils.FindDoorByIdInScene`, a utility to find a Bonfire by its local ID in a scene:

```csharp
// Assets/Game/Features/Interactive/Bonfire/BonfireUtils.cs
public static class BonfireUtils {
    public static List<Bonfire> GetBonfiresInScene(Scene scene) {
        var result = new List<Bonfire>();
        if (!scene.IsValid()) {
            return result;
        }
        var roots = scene.GetRootGameObjects();
        for (var i = 0; i < roots.Length; i++) {
            result.AddRange(roots[i].GetComponentsInChildren<Bonfire>(true));
        }
        return result;
    }

    public static Bonfire FindByIdInScene(Scene scene, string localId) {
        var bonfires = GetBonfiresInScene(scene);
        for (var i = 0; i < bonfires.Count; i++) {
            if (string.Equals(bonfires[i].CheckpointId, localId, StringComparison.Ordinal)) {
                return bonfires[i];
            }
        }
        return null;
    }
}
```

Used by the respawn flow and by editor validators.

### 3b. Respawn Flow

The respawn flow changes because the spawn position is no longer stored — it must be read from the Bonfire after the scene is loaded.

#### Same-scene respawn

```csharp
// PlayerController.WaitAndRespawnAtCheckpoint (same-scene branch)
var checkpoint = G.Checkpoint.Current.Value;
var bonfire = BonfireUtils.FindByIdInScene(gameObject.scene, checkpoint.Ref.LocalId);
RespawnAtPosition(bonfire.GetSpawnPosition());
```

#### Cross-scene respawn

When the checkpoint is in a different scene, the current flow is:
1. `G.Checkpoint.RequestRespawn()` — sets `HasPendingRespawn = true`
2. `SceneManager.LoadScene(sceneName)` — loads the target scene
3. New `PlayerController.Start()` — checks `HasPendingRespawn`, consumes it, teleports to stored position

With no stored position, step 3 becomes:
1. `PlayerController.Start()` checks `HasPendingRespawn`.
2. Consumes the pending respawn to get the `CheckpointRef`.
3. Finds the Bonfire by `localId` in the current scene.
4. Teleports to `bonfire.GetSpawnPosition()`.

```csharp
// PlayerController.Start()
private void Start() {
    if (G.Checkpoint.HasPendingRespawn) {
        var checkpointRef = G.Checkpoint.ConsumePendingRespawn();
        var bonfire = BonfireUtils.FindByIdInScene(gameObject.scene, checkpointRef.LocalId);
        if (bonfire != null) {
            transform.position = bonfire.GetSpawnPosition();
        }
        RestoreHealthAfterRespawn();
        damageable.IgnoreDamage = false;
        Actions.Enable();
    }
}
```

`ConsumePendingRespawn` returns `CheckpointRef` instead of `Vector2`.

### 4. SceneReference Helper

Add a static factory method for constructing a `SceneReference` from the active scene at runtime:

```csharp
// SceneReference.cs — new runtime method
public static SceneReference FromActiveScene() {
    var scene = SceneManager.GetActiveScene();
    // At runtime we don't have AssetDatabase, so GUID is not available.
    // Store scene path and name; GUID will be empty at runtime.
    // For save/load, the scene name is what SceneManager.LoadScene needs.
    return new SceneReference(sceneGuid: "", scenePath: scene.path);
}
```

**Problem:** at runtime, `AssetDatabase` is unavailable, so we can't get the scene GUID. The GUID is only useful in the editor. At runtime, `SceneManager.LoadScene` works with scene name or build index.

**Revised approach:** `SceneReference` stores whatever is available:
- In editor (when the Bonfire is placed): GUID + path are both set via `EditorSetFromSceneAsset`.
- At runtime (when the Bonfire is activated): only the scene path/name is available.

For save/load this is fine — the save file stores the scene name (needed for `SceneManager.LoadScene`). The GUID in `SceneReference` is a bonus for editor-time validation and repair, not a runtime requirement.

Simpler alternative: since Bonfire already lives in a scene, it can construct the reference from its own scene at activation time without needing a new factory. The path is `gameObject.scene.path`, and in editor mode the GUID can be resolved. At runtime, `scene.path` may be empty for additively loaded scenes, but `scene.name` is always available.

```csharp
// In Bonfire.DoInteract
var scene = gameObject.scene;
var ref = new CheckpointRef {
    Scene = SceneReference.FromScene(scene),
    LocalId = checkpointId,
};
```

```csharp
// SceneReference.cs
public static SceneReference FromScene(UnityEngine.SceneManagement.Scene scene) {
    return new SceneReference(sceneGuid: "", scenePath: scene.path, sceneName: scene.name);
}
```

Since `SceneReference` currently stores `sceneGuid` and `scenePath`, and `GetSceneName()` extracts the name from the path, this works as long as `scene.path` is set. For runtime-only scenarios where path might be empty, add a `sceneName` fallback field or just use `scene.name` directly.

**Decision:** Keep `SceneReference` as-is (guid + path). Add `FromScene` factory that sets the path from the runtime scene. `GetSceneName()` already extracts the name from the path. For runtime scenes where `scene.path` might be empty, fall back to `scene.name`. This is a minimal change to the existing struct.

### 5. Shared Infrastructure to Extract

#### IdUtils (from DoorIdUtils)

Move `GenerateId` and `IsValidId` to a shared utility. Doors and Bonfires both use the same ID format: `[0-9a-zA-Z_-]`, length 1..64.

```
Assets/Game/Core/Utils/IdUtils.cs        (new, extracted from DoorIdUtils)
Assets/Game/Features/Doors/DoorIdUtils.cs (becomes a thin wrapper or removed)
```

#### EditorSceneUtils (from DoorEditorUtils)

`ExecuteInScene` is already fully generic. Extract to shared editor utils.

```
Assets/Game/Core/Editor/EditorSceneUtils.cs (new, extracted from DoorEditorUtils)
```

`DoorEditorUtils.GetSceneGuid` is a one-liner and can stay or be inlined.

#### SceneReference

Already generic. Lives in `Assets/Game/Features/Doors/SceneReference.cs`. Moving it risks breaking serialized references on existing Doors. Leave it in place for now; Bonfire code references it via `using Game.Features.Doors`. Can be moved to `Assets/Game/Core/` in a future cleanup with proper `git mv` + meta handling.

### 6. Bonfire ID Auto-Generation

Same pattern as `Door.OnValidate`:

```csharp
// Bonfire.cs
#if UNITY_EDITOR
private const int DefaultGeneratedLength = 5;

private void OnValidate() {
    if (Application.isPlaying) {
        return;
    }

    if (PrefabUtility.IsPartOfPrefabAsset(this)) {
        if (!string.IsNullOrEmpty(checkpointId)) {
            checkpointId = string.Empty;
            EditorUtility.SetDirty(this);
        }
        return;
    }

    var source = PrefabUtility.GetCorrespondingObjectFromSource(this) as Bonfire;
    var isInheritedFromPrefab =
        source != null && string.Equals(checkpointId, source.checkpointId, StringComparison.Ordinal);

    if (string.IsNullOrWhiteSpace(checkpointId) || isInheritedFromPrefab) {
        checkpointId = $"Bonfire_{IdUtils.GenerateId(DefaultGeneratedLength)}";
        EditorUtility.SetDirty(this);
    }
}
#endif
```

Replaces the current `OnValidate` that only warns on empty. Every new Bonfire instance gets a unique ID automatically. Existing manually assigned IDs are preserved.

### 7. Per-Scene Uniqueness Validation

Since uniqueness is per-scene (same as doors), the validation structure mirrors Door exactly.

#### CheckpointValidator (Editor)

```
Assets/Game/Features/Interactive/Bonfire/Editor/CheckpointValidator.cs
```

```csharp
public static class CheckpointValidator {
    // Validates a single scene: ID format + per-scene uniqueness (same scope as DoorValidator).
    public static List<ValidationError> ValidateScene(Scene scene) {
        // 1. Find all Bonfire components in scene
        // 2. Check ID format (IdUtils.IsValidId)
        // 3. Check for duplicate local IDs within the scene
    }

    public static bool IsCheckpointIdUniqueInScene(Scene scene, Bonfire except, string localId) {
        // Same pattern as DoorValidator.IsDoorIdUniqueInScene
    }
}
```

#### CheckpointBuildValidator

```
Assets/Game/Features/Interactive/Bonfire/Editor/CheckpointBuildValidator.cs
```

`IPreprocessBuildWithReport` — iterates all scenes, calls `CheckpointValidator.ValidateScene` on each. Fails the build on invalid or duplicate IDs. Same pattern as `DoorBuildValidator`.

#### CheckpointPlayModeValidator

```
Assets/Game/Features/Interactive/Bonfire/Editor/CheckpointPlayModeValidator.cs
```

Validates open scenes before entering Play mode. Same pattern as `DoorPlayModeValidator`.

#### Validation Menu

```
Assets/Game/Features/Interactive/Bonfire/Editor/CheckpointValidationMenu.cs
```

`Tools/Checkpoints/Validate Open Scenes` — validates currently open scenes on demand.

### 8. Custom Inspector for Bonfire

```
Assets/Game/Features/Interactive/Bonfire/Editor/BonfireEditor.cs
```

Same pattern as `DoorEditor`:
- Read-only checkpoint ID field.
- "Copy ID" button.
- "Change ID" button (simpler than doors — no cross-references to update since nothing points to a bonfire by local ID from other objects, just validate per-scene uniqueness and rename).

### 9. Save/Load (Future)

When save/load is implemented, the save file stores only composite keys — no positions:

```json
{
  "currentCheckpoint": {
    "sceneName": "IntroLevel",
    "localId": "Bonfire_a3x8k"
  },
  "discoveredCheckpoints": [
    { "sceneName": "IntroLevel", "localId": "Bonfire_a3x8k" },
    { "sceneName": "CaveLevel", "localId": "Bonfire_k9m2p" }
  ]
}
```

On load:
1. Look up `sceneName` — this is what `SceneManager.LoadScene` needs.
2. After scene loads, find the Bonfire by `localId` via `BonfireUtils.FindByIdInScene`.
3. Read `bonfire.GetSpawnPosition()` — always the current position from the scene.
4. If the bonfire was moved between game updates, the player spawns at the new position automatically.
5. If the bonfire was deleted and re-created with the same `localId`, it still works.

No lookup table, no registry, no stale coordinates. The composite key carries the scene info; the live object provides the position.

## Summary of File Changes

### New Files

| File | Purpose |
|---|---|
| `Assets/Game/Core/Utils/IdUtils.cs` | Shared ID generation and validation (extracted from DoorIdUtils) |
| `Assets/Game/Core/Editor/EditorSceneUtils.cs` | Shared `ExecuteInScene` helper (extracted from DoorEditorUtils) |
| `Assets/Game/Core/Services/CheckpointRef.cs` | Composite key struct: SceneReference + LocalId |
| `Assets/Game/Features/Interactive/Bonfire/BonfireUtils.cs` | Find bonfires by ID in a scene (mirrors DoorUtils) |
| `Assets/Game/Features/Interactive/Bonfire/Editor/BonfireEditor.cs` | Custom inspector with read-only ID, Copy/Change |
| `Assets/Game/Features/Interactive/Bonfire/Editor/CheckpointValidator.cs` | Per-scene uniqueness validation |
| `Assets/Game/Features/Interactive/Bonfire/Editor/CheckpointBuildValidator.cs` | Fails build on invalid/duplicate IDs |
| `Assets/Game/Features/Interactive/Bonfire/Editor/CheckpointPlayModeValidator.cs` | Validates open scenes before Play |
| `Assets/Game/Features/Interactive/Bonfire/Editor/CheckpointValidationMenu.cs` | Manual validation via Tools menu |

### Modified Files

| File | Change |
|---|---|
| `Assets/Game/Core/Services/CheckpointService.cs` | `CheckpointData` drops `SpawnPosition` and `SceneName`, uses `CheckpointRef`; `discovered` set stores composite strings; `Activate` takes `CheckpointRef`; `ConsumePendingRespawn` returns `CheckpointRef` |
| `Assets/Game/Features/Interactive/Bonfire/Bonfire.cs` | Add `GetSpawnPosition()`; replace `OnValidate` with auto-generation; construct `CheckpointRef` on interact |
| `Assets/Game/Features/Doors/SceneReference.cs` | Add `FromScene` static factory; add `sceneName` fallback for runtime |
| `Assets/Game/Features/Doors/DoorIdUtils.cs` | Becomes wrapper around `IdUtils` (or removed) |
| `Assets/Game/Features/Doors/Editor/DoorEditorUtils.cs` | `ExecuteInScene` extracted to shared `EditorSceneUtils` |
| `Assets/Game/Features/Characters/Hero/PlayerController.cs` | Cross-scene respawn uses `BonfireUtils.FindByIdInScene` + `GetSpawnPosition()` instead of stored position |

## Implementation Order

1. **Extract shared utils** (`IdUtils`, `EditorSceneUtils`). Update Door code to use them. Verify doors still work.
2. **Add `SceneReference.FromScene`** factory method. Minimal change, no existing behavior affected.
3. **Create `CheckpointRef` and `BonfireUtils`**. Add `Bonfire.GetSpawnPosition()`.
4. **Refactor `CheckpointData`** — drop `SpawnPosition` and `SceneName`, use `CheckpointRef`. Update `CheckpointService.Activate` and `ConsumePendingRespawn` signatures.
5. **Update `Bonfire.DoInteract`** to construct and pass `CheckpointRef`.
6. **Update `PlayerController`** respawn flow: use `BonfireUtils.FindByIdInScene` + `GetSpawnPosition()`. Verify cross-scene respawn still works.
7. **Bonfire `OnValidate` auto-generation**. Verify new bonfire instances get unique IDs, existing ones preserved.
8. **Per-scene validator** + play-mode validator + build validator. Verify duplicate IDs are caught.
9. **Custom Bonfire inspector** (`BonfireEditor`). Read-only ID, Copy, Change buttons.

## Risks

**Extracting shared utils touches Door code.** Keep `DoorIdUtils` as a forwarding wrapper initially to minimize blast radius. Same for `DoorEditorUtils`.

**`SceneReference.FromScene` at runtime.** `scene.path` may be empty for additively loaded scenes in some Unity versions. Mitigation: add a `sceneName` fallback field, or use `scene.name` directly when `path` is empty. `GetSceneName()` should handle both.

**`discovered` set needs composite keys.** Storing `"{sceneGuid}:{localId}"` strings is simple but loses type safety. Acceptable for now; can be upgraded to a proper struct with equality if needed.

**Bonfire not found at respawn time.** If a bonfire is deleted from a scene but the save file still references its `localId`, `BonfireUtils.FindByIdInScene` returns null. The respawn code must handle this gracefully — e.g. log a warning and fall back to the scene's default spawn point or skip teleportation. This is a conscious trade-off: stale positions silently work but may be wrong; missing bonfires fail explicitly but are easier to diagnose.

**Existing Bonfire instances keep their current IDs.** `OnValidate` auto-generation only triggers for blank or prefab-inherited IDs. Manually assigned IDs are preserved.

**Moving `SceneReference` is deferred.** It stays in `Assets/Game/Features/Doors/` to avoid breaking serialized door references. Bonfire imports it via `using Game.Features.Doors`. Cosmetic, no functional impact.

## Editor Steps After Implementation

1. Open each scene with bonfires. Verify each bonfire has a unique auto-generated ID (or the manually assigned one is preserved).
2. Run `Tools/Checkpoints/Validate Open Scenes` to confirm no duplicates within each scene.
3. Verify cross-scene respawn still works in Play mode.