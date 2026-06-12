# Debug Inventory System

Debug-only tooling for seeding and inspecting the player inventory. Replaces the older
per-scene `DebugInitialInventory` MonoBehaviour with a single global ScriptableObject plus a
custom inspector that doubles as a live play-mode inventory editor.

## Pieces

| Piece | File | Role |
|---|---|---|
| `DebugInventoryConfig` | `Assets/Game/Core/DebugTools/DebugInventory/DebugInventoryConfig.cs` | SO holding the initial-items list. Loaded from `Resources/Debug/DebugInventoryConfig`. |
| Custom editor | `.../DebugInventory/Editor/DebugInventoryConfigEditor.cs` | Shared edit/play inspector UI. |
| `DebugSystemsConfig` | `Assets/Game/Configs/MainConfig.cs` | `EnableDebugInventory` toggle (under the Debug header). |
| `DebugSystemsLoader` | `Assets/Game/Core/DebugTools/DebugSystemsLoader.cs` | Applies enabled debug systems at startup; warns when any are active. |

## Enabling

Enablement lives in `MainConfig`, **not** on the SO:

`MainConfig → Debug Systems → Enable Debug Inventory`.

When off, nothing loads and there is zero runtime effect (production behavior). The SO editor
shows the current status and a button to ping `MainConfig`.

## Runtime behavior

`GInit.Awake` calls `DebugSystemsLoader.Apply(mainConfig)` right after `G.Game.Init()` (once the
player state exists). For each enabled system the loader applies it and records its name; if any
are active it logs a single warning:

```
[DebugSystems] ACTIVE debug systems (disable before shipping): Debug Inventory
```

For the inventory system specifically, it loads `DebugInventoryConfig` from Resources and calls
`InventoryModel.Add(itemId, count)` for each valid entry. Seeding happens **once at game start**;
it does not re-seed on respawn or checkpoint restore. A missing asset (toggle on, no asset) logs
an error and is skipped.

## Editor behavior

The inspector switches its source of truth by play state:

- **Edit mode** — reads/writes the serialized `initialItems` list (the startup seed).
- **Play mode** — reads/writes the live `G.Game.playerState.InventoryModel`. Pickups appear
  immediately (`RequiresConstantRepaint` while playing); Add/Remove act like pickups/drops.

Each row shows the item icon (from `ItemDef.Icon`, atlas-safe), id, and count, with a compact
remove (`X`) button; a `Clear All` button empties the list. The Add row is a dropdown of all
`DefsFacade` item ids, a count field, and an Add button (stacks onto an existing entry).

## Notes / limits

- The asset ships inside `Resources`. The runtime warning is the safety net; a compile-macro
  auto-disable for production builds is a future follow-up.
- `InventoryModel.Items` (read-only, unfiltered) was added to support the live view without the
  type filtering that `GetAll` applies.
