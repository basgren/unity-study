# 07 - Scene State Saving

## Goal

Persist selected state of scene objects across scene transitions and across bonfire rests, so the
world remembers what the player has done. Examples:

1. **Helm** (`Switch` component) — disabled state must persist after activation.
2. **StoneDoor** (`Switchable`) — once opened, stays open.
3. **Destructible Barrels** (`Damageable`) — once destroyed, do not reappear.
4. **DraggableBarrel** — keeps the position the player left it at.

There are two separate persistence tiers:

| Tier | Lifetime | Examples |
|---|---|---|
| **Session** | Until player rests at a bonfire | Moved barrels, partially completed encounters, transient destruction |
| **Persistent** | Until the end of the game | Opened StoneDoors, used Helms, permanently destroyed objects |

The configuration interface must be the same for both tiers — only a single field decides where the
state lives. Storage is in-memory for now, but the architecture must allow extending to disk save
files later. The service must therefore be able to dump/load the full set of object states as a
single JSON-friendly object that can be embedded in a larger save file.

The system should:

- be drop-in for common properties (existence, position, switch state, damageable health),
- be easy to extend with **custom** state for one-off objects,
- not require touching the service when adding a new kind of saved property.

## Current State

- **No save system exists.** All scene state lives in components and is lost on scene reload.
- **`PlayerState`** (`Assets/Game/Features/Characters/Hero/PlayerState.cs`) is the only piece of
  state that survives scene loads, and only because `GameManager` is `DontDestroyOnLoad`. It has a
  generic `flags` set, but it is per-player, not per-scene-object.
- **`CheckpointService`** (`Assets/Game/Core/Services/CheckpointService.cs`) keeps a set of
  discovered checkpoint IDs and the current checkpoint. It already follows the pattern for
  `[scene]:[localId]` composite keys (`MakeKey`) and is registered through `GInit`. The new service
  follows the same conventions.
- **Stable IDs already exist for two systems**:
  - `Door.doorId` — assigned via `OnValidate`, regenerated for prefab instances using
    `IdUtils.GenerateId` (`Assets/Game/Features/Doors/Door.cs:135-161`).
  - `Bonfire.checkpointId` — same pattern (`Assets/Game/Features/Interactive/Bonfire/Bonfire.cs:147-181`).
  - The new state savers reuse this convention so the user experience is identical.
- **`Switch`** (`Assets/Game/Core/Components/Interaction/Switch.cs`) exposes a serialized
  `isDisabled` field that already represents Helm "used" state, and a `SingleUse` mode that sets
  it. We do not need to change `Switch` itself — only observe it.
- **`Switchable`** (`Assets/Game/Core/Components/Interaction/Switchable.cs`) exposes `IsActive`
  with a change event — easy to capture for StoneDoor.
- **`Damageable`** has `OnHealthChanged` and an `IsDead` flag — easy to capture/destroy hook.
- **`DraggableBarrel`** is plain physics on `Rigidbody2D` — only `transform.position` matters.

## Design Constraints (from AGENTS.md and existing code)

- Service is created dynamically in `GInit`. **No `[SerializeField]` on the service** — any
  configuration goes on `MainConfig`.
- IDs are stable strings, generated via `OnValidate` for prefab instances, never inherited from a
  prefab asset. Same pattern as `Door` and `Bonfire`.
- Saved data is keyed by `(sceneId, localId)` where `sceneId` is **stable across scene
  rename/move** — see "Scene Identity" below.

## Scene Identity (Rename-Safe Keys)

Using `SceneManager.GetActiveScene().name` as a primary key in saved data is fragile. Renaming
`level1.unity` to `pirate_cove.unity` would orphan every saved blob.

The project **already has** a primitive that solves this: `SceneReference`
(`Assets/Game/Features/Doors/SceneReference.cs`). It stores a Unity **asset GUID** plus the
project path, and the GUID lives in the scene's `.meta` file — stable across both rename and move.
It is already used by `Door.link.TargetScene` and by `CheckpointRef.Scene`, so the same type
should hold all "scene identity" responsibilities, including state-save keys. We do not need to
invent a `SceneId` struct.

### Moving `SceneReference` to Core (precondition)

`SceneReference` currently lives under `Assets/Game/Features/Doors/`, but it is already imported
from `Game.Core.Services.CheckpointRef` — a Core → Features dependency, which is the wrong
direction. The state system would deepen that inversion. Move the file (and its `.meta`!) before
anything else:

```
git mv Assets/Game/Features/Doors/SceneReference.cs       Assets/Game/Core/Services/Scene/SceneReference.cs
git mv Assets/Game/Features/Doors/SceneReference.cs.meta  Assets/Game/Core/Services/Scene/SceneReference.cs.meta
```

Update the namespace from `Game.Features.Doors` to `Game.Core.Services.Scene` in the file itself,
then sweep `using` directives in the small number of consumers (`Door.cs`, `DoorLink.cs`,
`CheckpointRef.cs`, `Bonfire.cs`, `MainMenu.cs`). The GUID stays the same, so no scene/prefab
re-wiring is required.

### Why a Catalog Is Still Needed

`SceneReference` knows its GUID **at edit time** (set via `EditorSetFromSceneAsset`). At runtime,
however, `SceneManager.GetActiveScene()` returns only a `Scene` struct — no GUID exposed by Unity.
`SceneReference.FromScene(runtimeScene)` even leaves `sceneGuid` empty for exactly this reason.

So we still need a path → GUID lookup table built at edit time and shipped with the game. The
twist: instead of inventing a new `Entry` struct, the catalog can store an array of
`SceneReference` directly.

### `SceneCatalog` ScriptableObject

```csharp
// Assets/Game/Core/Services/SceneState/SceneCatalog.cs
[CreateAssetMenu(menuName = "Config/Scene Catalog", fileName = "SceneCatalog")]
public sealed class SceneCatalog : ScriptableObject {
    [SerializeField] private SceneReference[] scenes;

    private Dictionary<string, SceneReference> byPath;
    private Dictionary<string, SceneReference> byGuid;

    public void RebuildIndex() {
        byPath = new Dictionary<string, SceneReference>(scenes.Length);
        byGuid = new Dictionary<string, SceneReference>(scenes.Length);
        foreach (var s in scenes) {
            if (!string.IsNullOrEmpty(s.ScenePath)) {
                byPath[s.ScenePath] = s;
            }
            if (!string.IsNullOrEmpty(s.SceneGuid)) {
                byGuid[s.SceneGuid] = s;
            }
        }
    }

    /// <summary>Resolves a runtime scene to its rename-safe SceneReference.</summary>
    public bool TryResolve(Scene runtimeScene, out SceneReference result) {
        if (byPath == null) {
            RebuildIndex();
        }
        return byPath.TryGetValue(runtimeScene.path, out result);
    }

    public bool TryGetByGuid(string guid, out SceneReference result) {
        if (byGuid == null) {
            RebuildIndex();
        }
        return byGuid.TryGetValue(guid, out result);
    }
}
```

The editor builder runs on demand and on build via `IPreprocessBuildWithReport`, scanning
`EditorBuildSettings.scenes` and calling `EditorSetFromSceneAsset` on each entry.

`MainConfig.SceneCatalog` is the configuration reference (per the service-config rules: services
created via `GInit` must not use `[SerializeField]` for shared assets).

### Where `SceneReference` Plugs Into the State System

- The `SceneStateService` keys its stores by `SceneReference.SceneGuid` (a string), not by a
  separate wrapper type. `SceneReference` is the typed handle that callers pass around; the GUID
  is the bare key inside the dictionaries and inside the JSON snapshot.
- `CheckpointRef.Scene` already uses `SceneReference` — same type, same conventions, no second
  parallel hierarchy.
- Reusing `SceneReference` also means the existing editor support (custom property drawers, scene
  picker via `EditorGetSceneAsset`) carries over to anything in the state system that needs to
  reference a specific scene at edit time.

### Fallback / Diagnostics

- If a scene is not in the catalog (e.g. an out-of-build editor test scene), `GetSceneId` returns
  an invalid `SceneId` and the service logs a single warning per scene. Capture/restore become
  no-ops for that scene rather than poisoning the save with path-keyed data.
- If a save file references a GUID that the catalog no longer knows about (scene deleted),
  `ImportSnapshot` keeps the orphan blob in a "quarantined" sub-store so it survives subsequent
  `ExportSnapshot` calls and is never silently lost.

### What Still Breaks Saves

| Edit | Saves preserved? |
|---|---|
| Rename a scene file | Yes (GUID survives) |
| Move a scene to another folder | Yes (GUID survives) |
| Delete a scene | No (GUID is gone) — orphan quarantined |
| Duplicate a scene asset | New GUID; old data sticks to the original |
| Re-import a scene with "Reset GUIDs" | Loses link — same risk as `.meta` deletion |
| Rename/move a saveable object's `StateRoot.saveId` | No (object loses its identity) — same as Doors today |

This matches the existing rules around `.meta` files (AGENTS.md: "always move its `.meta` file
together"). Stable identity comes from `.meta` GUIDs in both directions.

## Centralized Scene Loading

Today, `SceneManager.LoadScene` is called from at least seven places:

```
Assets/Game/Features/Doors/DoorTravelService.cs:40           — door travel (already centralized for doors)
Assets/Game/Features/Characters/Hero/PlayerController.cs:603 — death restart
Assets/Game/Features/Characters/Hero/PlayerController.cs:645 — checkpoint cross-scene respawn
Assets/Game/Core/Components/SceneManagement/ReloadLevelComponent.cs:9 — debug/event reload
Assets/Game/UI/MainMenu/MainMenu.cs:14                       — start game
Assets/Game/UI/PauseMenu/PauseMenu.cs:9                      — back to main menu
Assets/Game/Core/Services/Scene/HudService.cs:26             — additive HUD load (special case)
```

For the state system to be reliable, **every** non-additive transition must give the service a
chance to capture state from the outgoing scene. The cleanest solution is the one the user
suggested: a single `SceneTravelService` that owns all `LoadScene` calls, fires lifecycle events,
and offers a small surface for callers.

### `SceneTravelService`

```csharp
// Assets/Game/Core/Services/Scene/SceneTravelService.cs
public class SceneTravelService : MonoBehaviour {
    public event Action<Scene> BeforeUnload;          // last chance to capture state
    public event Action<Scene> AfterLoad;             // after Awake, before first Start
    public event Action<Scene, Scene> AfterTransition; // (from, to)

    public void LoadScene(SceneId sceneId, SceneLoadOptions options = default) { ... }
    public void LoadScene(string sceneName, SceneLoadOptions options = default) { ... }
    public void ReloadActiveScene(SceneLoadOptions options = default) { ... }

    private IEnumerator LoadRoutine(string targetSceneName, SceneLoadOptions options) {
        var fromScene = SceneManager.GetActiveScene();
        BeforeUnload?.Invoke(fromScene);

        var op = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
        while (!op.isDone) {
            yield return null;
        }

        var toScene = SceneManager.GetSceneByName(targetSceneName);
        AfterLoad?.Invoke(toScene);
        AfterTransition?.Invoke(fromScene, toScene);
    }
}
```

`SceneLoadOptions` can carry an optional fade duration, a "skip capture" flag for special cases
(e.g. main menu), and a callback for post-load logic.

### Hookup

`SceneStateService` subscribes to `BeforeUnload` once in `Awake` and runs its
`CaptureActiveScene` pass there. No call sites need to know about state saving.

```csharp
// SceneStateService.Awake (after G is wired up):
G.SceneTravel.BeforeUnload += OnBeforeUnload;

private void OnBeforeUnload(Scene scene) {
    CaptureScene(scene);
}
```

### Migration of Existing Call Sites

| Call site | Current | Replacement |
|---|---|---|
| `DoorTravelService.LoadSceneAndTeleportPlayer` | `SceneManager.LoadSceneAsync(sceneName, ...)` | `G.SceneTravel.LoadScene(sceneName, options)` |
| `PlayerController:603` death restart | `SceneManager.LoadScene(currentScene)` | `G.SceneTravel.ReloadActiveScene()` |
| `PlayerController:645` checkpoint respawn | `SceneManager.LoadScene(checkpointSceneName)` | `G.SceneTravel.LoadScene(checkpointSceneId)` |
| `ReloadLevelComponent` | `SceneManager.LoadScene(active.name)` | `G.SceneTravel.ReloadActiveScene()` |
| `MainMenu.cs` start game | `SceneManager.LoadScene(startScene.GetSceneName())` | `G.SceneTravel.LoadScene(SceneId.From(startScene))` |
| `PauseMenu.cs` back to main | `SceneManager.LoadScene("MainMenu")` | `G.SceneTravel.LoadScene("MainMenu", new SceneLoadOptions { SkipStateCapture = true })` |
| `HudService.cs` additive HUD | `LoadSceneAsync(..., Additive)` | **Unchanged.** Additive load is a separate concern; stays direct or gets a tiny `LoadAdditive` helper. |

After migration there is exactly one place to grep for `SceneManager.LoadScene` outside the
`HudService` HUD load — the travel service itself. New features hook lifecycle events instead of
adding new call sites.

### Why Not Just Subscribe to `SceneManager.sceneUnloaded`?

`sceneUnloaded` fires *after* `OnDestroy` of every component in the scene, so by the time it runs,
all `MonoBehaviour` references in the captured StateRoots are dangling. We need a hook that fires
*before* `LoadScene` is called, while the outgoing scene is still alive — that is precisely what
`SceneTravelService.BeforeUnload` provides.

## Approaches Considered

### Option A — Centralized Self-Registering Components

Each saveable component implements `IStateSaver` and registers itself in `OnEnable`:

```csharp
public interface IStateSaver {
    string SaveId { get; }
    SaveTier Tier { get; }
    void Capture(IStateWriter w);
    void Restore(IStateReader r);
}
```

The service keeps a dictionary `key -> blob`, asks every registered saver to `Capture` on scene
unload, and pushes blobs back via `Restore` on scene load.

**Pros:**
- Minimal hierarchy — one component per saveable.
- No marker GameObject required.

**Cons:**
- Each saveable component must own the ID, which means duplicating the
  `OnValidate`/auto-generate boilerplate everywhere.
- One object that needs *both* "save my position" and "save my switch state" needs two components,
  each carrying its own ID — error-prone.
- Hard to mix common and custom state on the same object without writing one big monolithic saver.

### Option B — `StateRoot` + Composable Savers (Recommended)

A single `StateRoot` component per saveable GameObject owns the ID and the tier. **Sibling**
components implement `IStateSaver` and contribute one *slot* each. Common reusable savers live in
`Core`; custom ones are project-specific.

```
Helm GameObject:
├── Switch                    (existing gameplay)
├── HelmController            (existing gameplay)
├── StateRoot                 ← id: "Helm_a1b2", tier: Persistent
└── SwitchStateSaver          ← slot: "switch"

DraggableBarrel GameObject:
├── DraggableBarrel
├── StateRoot                 ← id: "Barrel_x9k2", tier: Session
├── TransformStateSaver       ← slot: "transform"
└── ExistenceStateSaver       ← slot: "existence"   (only if it can be destroyed too)
```

**Pros:**
- One ID per object, one place to put it (`StateRoot`).
- Drop-in reusable savers for common properties.
- Mix common + custom on the same object freely.
- Inspector reveals exactly what state will be saved.
- ID auto-generation lives in one place.

**Cons:**
- Two components instead of one for the simplest case.
- Slightly more boilerplate to define a "slot" key.

**Recommendation: Option B.** It matches the existing `Switch` + `Switchable` composition style
and the user requirement to mix common and custom properties naturally.

### Option C — ScriptableObject Snapshot Per Object Type

Each saveable type writes a typed `ScriptableObject` snapshot. Rejected: snapshots and runtime
data must live in different objects, JSON serialization is awkward, and the type per object hurts
extensibility (every new property requires a new SO type).

## Recommended Architecture (Option B)

### Files

```
Assets/Game/Core/Services/SceneState/
    SceneStateService.cs       — global service, registered in G/GInit
    SaveTier.cs                — enum: Session, Persistent
    StateRoot.cs               — id + tier, owns the slot dictionary
    IStateSaver.cs             — slot contributor interface
    StateBlob.cs               — JSON-friendly per-object data structure
    Savers/
        TransformStateSaver.cs
        ExistenceStateSaver.cs
        SwitchStateSaver.cs
        SwitchableStateSaver.cs
        DamageableStateSaver.cs
```

### `SaveTier`

```csharp
namespace Game.Core.Services.SceneState {
    public enum SaveTier {
        /// Cleared when the player rests at a bonfire.
        Session,

        /// Persists for the entire playthrough.
        Persistent,
    }
}
```

### `StateRoot`

The marker component. Owns the ID, the tier, and the list of contributing savers. ID is
auto-generated for prefab instances using the same pattern as `Door`/`Bonfire`.

```csharp
[DisallowMultipleComponent]
public sealed class StateRoot : MonoBehaviour {
    [SerializeField, HideInInspector] private string saveId;
    [SerializeField] private SaveTier tier = SaveTier.Persistent;

    public string SaveId => saveId;
    public SaveTier Tier => tier;

    private IStateSaver[] savers;

    private void Awake() {
        savers = GetComponents<IStateSaver>();
    }

    private void Start() {
        // Pull state on Start so all sibling savers have finished their own Awake.
        G.SceneState.RestoreInto(this, savers);
    }

    /// <summary>Captures all slots from this object into the service buffer.</summary>
    internal void CaptureInto(StateBlob blob) {
        foreach (var s in savers) {
            s.Capture(blob.Writer(s.Slot));
        }
    }

    internal void ApplyFrom(StateBlob blob) {
        foreach (var s in savers) {
            if (blob.TryReader(s.Slot, out var r)) {
                s.Restore(r);
            }
        }
    }

#if UNITY_EDITOR
    private const int DefaultGeneratedLength = 5;

    private void OnValidate() {
        if (Application.isPlaying) {
            return;
        }

        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this)) {
            if (!string.IsNullOrEmpty(saveId)) {
                saveId = string.Empty;
                UnityEditor.EditorUtility.SetDirty(this);
            }
            return;
        }

        var source = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(this) as StateRoot;
        var inheritedFromPrefab =
            source != null && string.Equals(saveId, source.saveId, System.StringComparison.Ordinal);

        if (string.IsNullOrWhiteSpace(saveId) || inheritedFromPrefab) {
            saveId = $"State_{IdUtils.GenerateId(DefaultGeneratedLength)}";
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}
```

### `IStateSaver` and `StateBlob`

```csharp
public interface IStateSaver {
    /// Unique slot key within the StateRoot, e.g. "transform", "switch".
    string Slot { get; }

    /// Writes the current state to the writer.
    void Capture(IStateWriter w);

    /// Reads previously captured state and applies it.
    void Restore(IStateReader r);
}
```

`IStateWriter`/`IStateReader` are minimal abstractions over a typed key/value bag (string, int,
float, bool, Vector2, Vector3). No reflection — each saver is responsible for marshalling its own
fields. This keeps the format inspectable, debuggable, and trivially JSON-serializable.

A `StateBlob` is the in-memory form for one (saveId, tier) pair. It owns one `Dictionary<string,
Dictionary<string, object>>` where the outer key is the slot and the inner is the field name.
Conversion to JSON is one-step via Newtonsoft.Json (already common in Unity projects); the in-memory
shape is intentionally pure data so it can be merged into a larger save file later.

### `SceneStateService`

```csharp
public class SceneStateService : MonoBehaviour {
    // SceneId.Guid -> saveId -> StateBlob
    private readonly Dictionary<string, Dictionary<string, StateBlob>> sessionStore = new();
    private readonly Dictionary<string, Dictionary<string, StateBlob>> persistentStore = new();

    private SceneCatalog catalog;

    public void Init() {
        catalog = G.Config.SceneCatalog;
        G.SceneTravel.BeforeUnload += OnBeforeUnload;
    }

    private void OnBeforeUnload(Scene scene) {
        CaptureScene(scene);
    }

    /// <summary>Captures every StateRoot in the given scene.</summary>
    public void CaptureScene(Scene scene) {
        var sceneId = catalog.GuidForScene(scene);
        if (string.IsNullOrEmpty(sceneId)) {
            return; // Out-of-catalog scene; warning already logged.
        }
        foreach (var root in FindRootsInScene(scene)) {
            var store = StoreFor(root.Tier);
            var byId = store.GetOrCreate(sceneId);
            var blob = byId.GetOrCreate(root.SaveId);
            root.CaptureInto(blob);
        }
    }

    internal void RestoreInto(StateRoot root, IStateSaver[] savers) {
        var sceneId = catalog.GuidForScene(root.gameObject.scene);
        if (TryGetBlob(sceneId, root.SaveId, root.Tier, out var blob)) {
            root.ApplyFrom(blob);
        }
    }

    /// <summary>Called when the player rests at a bonfire.</summary>
    public void OnBonfireRest() {
        sessionStore.Clear();
        // Persistent store keeps all of its data.
    }

    /// <summary>JSON-friendly snapshot of all state, for embedding in a save file.</summary>
    public SceneStateSnapshot ExportSnapshot() { ... }
    public void ImportSnapshot(SceneStateSnapshot snapshot) { ... }
}
```

`FindRootsInScene` is the only "find all" call we make, and only at scene-transition boundaries —
this is acceptable per AGENTS.md (no per-frame global lookups).

### When Capture Happens

There are two capture moments:

1. **Push-on-event** — for state that changes through a clean event (`Switchable.OnChangeEvent`,
   `Damageable.OnHealthChanged`, "barrel destroyed" hook). The saver pushes the new value into the
   service immediately. This way, the state is correct even if the player force-quits.
2. **Snapshot-on-leave** — for state with no clean change event (`transform.position` of a moved
   barrel). The service runs `CaptureScene` from the `SceneTravelService.BeforeUnload` event,
   which fires before the outgoing scene is unloaded. See the "Centralized Scene Loading" section
   above — this is the only required hook, regardless of who initiated the transition.

### When Restore Happens

`StateRoot.Start()` pulls its own blob from the service and applies it through its savers. By that
point, scene objects have completed `Awake`, so physics colliders and renderers are ready.

This per-object pull avoids the timing pitfalls of trying to push from a service in
`SceneManager.sceneLoaded` (no guarantee about per-object Awake order).

### Common Savers — Examples

#### `TransformStateSaver`

Saves position. Optional flags for rotation/scale.

```csharp
public sealed class TransformStateSaver : MonoBehaviour, IStateSaver {
    [SerializeField] private bool saveRotation = false;
    public string Slot => "transform";

    public void Capture(IStateWriter w) {
        w.SetVector2("p", transform.position);
        if (saveRotation) {
            w.SetFloat("r", transform.eulerAngles.z);
        }
    }

    public void Restore(IStateReader r) {
        if (r.TryGetVector2("p", out var p)) {
            transform.position = p;
        }
        if (saveRotation && r.TryGetFloat("r", out var rot)) {
            transform.rotation = Quaternion.Euler(0, 0, rot);
        }
    }
}
```

#### `ExistenceStateSaver`

Saves whether the object should exist on next load. Default state in scene = exists. Saver only
flips it to "destroyed" — never resurrects, because the scene file already provides the object.

```csharp
public sealed class ExistenceStateSaver : MonoBehaviour, IStateSaver {
    public string Slot => "exists";
    private bool destroyed;

    /// <summary>Call instead of Destroy() so we can record the destruction.</summary>
    public void MarkDestroyedAndDestroy() {
        destroyed = true;
        // Push immediately so the state is captured even before scene unload.
        G.SceneState.PushSlot(GetComponent<StateRoot>(), Slot, w => w.SetBool("d", true));
        Destroy(gameObject);
    }

    public void Capture(IStateWriter w) { w.SetBool("d", destroyed); }

    public void Restore(IStateReader r) {
        if (r.TryGetBool("d", out var d) && d) {
            // The scene re-instantiated us; remove ourselves immediately.
            Destroy(gameObject);
        }
    }
}
```

The destroying caller (e.g. `Damageable.onDeath` UnityEvent) must call `MarkDestroyedAndDestroy`
instead of plain `Destroy`. This explicit hand-off avoids the ambiguity of "is OnDestroy a real
death or a scene unload".

#### `SwitchStateSaver` / `SwitchableStateSaver`

Saves `Switch.isDisabled` (Helm-style "used" flag) or `Switchable.IsActive` (StoneDoor open/close).
Captures on the existing `OnSwitchChangeEvent` and on snapshot-on-leave.

#### `DamageableStateSaver`

Saves `Damageable.currentHealth`. Useful when an enemy has been weakened but not killed and we
want that to persist until the next bonfire rest (`tier = Session`).

### Wiring Existing Objects

| Object | Tier | Savers |
|---|---|---|
| Helm | Persistent | `StateRoot` + `SwitchStateSaver` |
| StoneDoor | Persistent | `StateRoot` + `SwitchableStateSaver` |
| BarrelDestructable | Persistent (or Session) | `StateRoot` + `ExistenceStateSaver` |
| DraggableBarrel | Session | `StateRoot` + `TransformStateSaver` |
| Enemy (e.g. Pinky) | Session | `StateRoot` + `DamageableStateSaver` (and `ExistenceStateSaver` if killed enemies should not respawn until rest) |

The tier is set per-instance in the Inspector. The same prefab can be Session in one scene and
Persistent in another with no code changes — just override on the instance.

### Bonfire Integration

`Bonfire.DoInteract` already calls `G.Checkpoint.Activate(...)`. The new behavior:

```csharp
protected override void DoInteract() {
    if (bonfireState == BonfireState.Current) {
        return;
    }

    var checkpointRef = new CheckpointRef { ... };
    G.Checkpoint.Activate(checkpointRef);
    G.SceneState.OnBonfireRest();   // clears Session tier
    G.Checkpoint.RequestRespawn();
    G.SceneTravel.ReloadActiveScene();
}
```

Reloading the scene after rest is the simplest way to restore enemy positions, refilled barrels,
etc. — the Session store is empty, so all StateRoot.Start calls become no-ops and the scene
default state takes over. (The persistent store still re-applies opened doors and used helms.)

### Snapshot Format

Scenes are keyed by **GUID**, not by name — this is the rename-safe identifier from
`SceneCatalog`. A path is included only as a non-authoritative comment for human debugging when
the snapshot is exported pretty-printed (it is ignored on import).

```jsonc
{
    "version": 1,
    "session": {
        "5a3b...e91": {                      // SceneId.Guid (was: level1.unity)
            "Barrel_x9k2": {
                "transform": { "p": [12.3, 4.1] }
            }
        }
    },
    "persistent": {
        "5a3b...e91": {
            "Helm_a1b2":  { "switch":     { "disabled": true } },
            "Door_q3k7":  { "switchable": { "active":   true } },
            "Brl_z0p4":   { "exists":     { "d": true } }
        }
    },
    "quarantine": {
        // Blobs whose scene GUID is no longer in the catalog. Preserved on
        // round-trip so that re-adding the scene later restores its state.
    }
}
```

This object can be embedded as a child of a future global save object (alongside `PlayerState`,
`CheckpointService` data, inventory, etc.).

## File Changes

**New files:**

| File | Purpose |
|---|---|
| `Assets/Game/Core/Services/SceneState/SceneStateService.cs` | Service: in-memory store + capture/restore + JSON snapshot |
| `Assets/Game/Core/Services/SceneState/SaveTier.cs` | Enum |
| `Assets/Game/Core/Services/SceneState/SceneId.cs` | Wrapper around the scene GUID |
| `Assets/Game/Core/Services/SceneState/SceneCatalog.cs` | ScriptableObject mapping scene path ↔ GUID |
| `Assets/Game/Core/Services/SceneState/StateRoot.cs` | Per-object marker with auto-generated ID |
| `Assets/Game/Core/Services/SceneState/IStateSaver.cs` | Contributor interface + writer/reader interfaces |
| `Assets/Game/Core/Services/SceneState/StateBlob.cs` | In-memory data shape |
| `Assets/Game/Core/Services/SceneState/Savers/TransformStateSaver.cs` | Position (+ optional rotation) |
| `Assets/Game/Core/Services/SceneState/Savers/ExistenceStateSaver.cs` | Destroyed flag |
| `Assets/Game/Core/Services/SceneState/Savers/SwitchStateSaver.cs` | `Switch.isDisabled` |
| `Assets/Game/Core/Services/SceneState/Savers/SwitchableStateSaver.cs` | `Switchable.IsActive` |
| `Assets/Game/Core/Services/SceneState/Savers/DamageableStateSaver.cs` | `Damageable.currentHealth` |
| `Assets/Game/Editor/SceneState/SceneCatalogBuilder.cs` | Editor: rebuild catalog from `EditorBuildSettings.scenes` (menu item + build preprocessor) |
| `Assets/Game/Core/Services/Scene/SceneTravelService.cs` | Centralized scene loader with `BeforeUnload` / `AfterLoad` hooks |
| `Assets/Game/Resources/Configs/SceneCatalog.asset` | The catalog instance, referenced by `MainConfig` |

**Modified files:**

| File | Change |
|---|---|
| `Assets/Game/Core/Bootstrap/G.cs` | Add `SceneState` and `SceneTravel` properties |
| `Assets/Game/Core/Bootstrap/GInit.cs` | Create `SceneTravelService` and `SceneStateService`, call `SceneStateService.Init()` |
| `Assets/Game/Configs/MainConfig.cs` | Add `SceneCatalog SceneCatalog;` reference |
| `Assets/Game/Features/Doors/DoorTravelService.cs` | Replace `SceneManager.LoadSceneAsync` with `G.SceneTravel.LoadScene` |
| `Assets/Game/Features/Characters/Hero/PlayerController.cs` | Replace two `SceneManager.LoadScene` calls with `G.SceneTravel.ReloadActiveScene()` / `LoadScene` |
| `Assets/Game/Core/Components/SceneManagement/ReloadLevelComponent.cs` | Use `G.SceneTravel.ReloadActiveScene()` |
| `Assets/Game/UI/MainMenu/MainMenu.cs` | Use `G.SceneTravel.LoadScene` |
| `Assets/Game/UI/PauseMenu/PauseMenu.cs` | Use `G.SceneTravel.LoadScene` with `SkipStateCapture = true` |
| `Assets/Game/Features/Interactive/Bonfire/Bonfire.cs` | Call `G.SceneState.OnBonfireRest()`, then `G.SceneTravel.ReloadActiveScene()` |
| `Assets/Game/Features/Interactive/Helm/Helm.prefab` | Add `StateRoot` (Persistent) + `SwitchStateSaver` |
| `Assets/Game/Features/Doors/StoneDoor/StoneDoor.prefab` | Add `StateRoot` (Persistent) + `SwitchableStateSaver` |
| `Assets/Game/Features/Props/Barrel/BarrelDestructable.prefab` | Add `StateRoot` + `ExistenceStateSaver` |
| `Assets/Game/Features/Dynamic/Barrel.prefab` | Add `StateRoot` (Session) + `TransformStateSaver` |
| Destructible barrel destroy callback | Use `ExistenceStateSaver.MarkDestroyedAndDestroy` instead of `Destroy` |

## Unity Editor Steps

1. Create `SceneCatalog.asset` under `Assets/Game/Resources/Configs/`. Run **Tools → Scene State →
   Rebuild Scene Catalog** to populate it from build settings. Assign it to `MainConfig.SceneCatalog`.
2. Add `StateRoot` + the relevant saver components to the prefabs listed above. Each prefab leaves
   `saveId` empty; instances auto-generate IDs via `OnValidate`.
3. Set `tier` on each prefab to the desired default. Per-instance overrides are still possible.
4. For destructible barrels, change the death UnityEvent to call
   `ExistenceStateSaver.MarkDestroyedAndDestroy` instead of `GameObject.Destroy`.
5. Whenever a scene is added to or removed from build settings, re-run the catalog rebuild (or
   rely on the build preprocessor that runs it automatically before each build).

## Risks and Concerns

- **Capture timing on scene leave.** State capture is only correct if the transition goes through
  `SceneTravelService`. New code that calls `SceneManager.LoadScene` directly will silently lose
  unsaved transform-style state. Mitigation: a Roslyn analyzer or a simple grep-based unit test in
  `Assets/Game/Editor/Tests` that fails if `SceneManager.LoadScene` appears anywhere outside
  `SceneTravelService.cs` and the additive HUD load.
- **Restore vs. physics.** `TransformStateSaver` writes `transform.position` directly in `Start`.
  For barrels with stacked physics this can cause overlap or instant velocity. Mitigation:
  temporarily clear `Rigidbody2D.velocity` on restore. Document that `TransformStateSaver` is for
  static-ish props, not for in-flight projectiles.
- **Existence vs. instantiation.** `ExistenceStateSaver` only knows how to *destroy* objects on
  load — it cannot resurrect objects whose prefab no longer exists in the scene. This is fine for
  scene-placed objects but does not help spawned enemies. Spawned enemy persistence is a separate
  problem (the spawner itself should own a "killed list" — out of scope here).
- **Stale state after asset moves.** If an object's `saveId` is regenerated by `OnValidate` (e.g.
  someone unticks a prefab override), old saved blobs become orphans. Same risk already accepted
  for `Door.doorId` and `Bonfire.checkpointId`. Mitigation: an editor tool that scans for unknown
  IDs in saved data (later).
- **Duplicate IDs within a scene.** No compile-time enforcement. Risk identical to existing
  `Door`/`Bonfire`. Mitigation: editor scanner that warns on duplicate `StateRoot.saveId` per
  scene (later).
- **Save format versioning.** When new fields are added to a saver, old save files may not have
  them. Each `IStateSaver.Restore` should `TryGet` and tolerate missing fields. The snapshot
  carries a top-level `version` field to permit explicit migrations later.
- **Per-frame allocation.** None — capture and restore happen only at scene boundaries and on
  explicit gameplay events. No `Update` work.
- **Service order in `GInit`.** `SceneTravelService` must be created before `SceneStateService`
  so the latter can subscribe to `BeforeUnload` in its `Init()`. Both are created in `GInit.Awake`
  before any gameplay code runs, so the order is fully under our control.
- **Scene catalog drift.** If a developer adds a scene to build settings and forgets to rebuild
  the catalog, runtime lookups for that scene return an invalid `SceneId` and capture/restore
  silently no-op. Mitigation: a build-time `IPreprocessBuildWithReport` that rebuilds the catalog
  automatically, plus a play-mode validator that warns when the active scene's path is not in the
  catalog.
- **Inspector noise.** Two extra components per saveable object. Acceptable cost for the
  composability and the existing `Switch` / `Switchable` style.

## Future Extensions (Not in Scope Now)

- Disk save file: serialize `ExportSnapshot()` to JSON next to `PlayerState` and other game-wide
  state. The JSON shape above is already disk-ready.
- Editor scanner that lists all `StateRoot` IDs and flags duplicates.
- Spawner-owned persistence for dynamically spawned enemies.
- A `FlagStateSaver` for arbitrary boolean game flags addressed by name (e.g. "boss1Defeated"),
  for cases where there is no scene object to attach to.
- Session "migration on rest": instead of clearing the Session tier on rest, optionally promote
  certain slots into the Persistent tier (e.g. "discovered" without "used").