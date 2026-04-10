# Shop & Perk System

## Context

Rikko NPC already has a dialog (`rikko.json`) with an inline sword purchase. We need a proper shop UI that opens from dialog, where the player buys items. Two new purchasable perks (Protection Mask: 25 coins, Parrot: 50 coins) plus the existing Sword (10 coins, moved from inline dialog). Perks are a new item category with a separate HUD panel and separate input keys for activation/cycling.

## Key Decisions

- **1 key cycles items forward** (replaces prev/next), **2 cycles perks forward**
- **A activates item**, **S activates perk**
- Sword moves into shop (remove inline dialog purchase)
- Protection Mask strategy is a stub for now
- New `ItemType.Perk` enum value; `PerkPanelModel` mirrors `BackpackPanelModel`
- Separate `ItemUseService` instance for perks (avoids coupling the two panel models)
- New `DialogActionType.OpenShop` handled in `DialogService`
- `ShopDef` ScriptableObject defines what a merchant sells

---

## Phase 1 -- Data Layer

### 1.1 Add `ItemType.Perk`

**File:** `Assets/Game/Core/Models/Inventory/InventoryItemsDef.cs`

Add `Perk` to the `ItemType` enum after `Instant`:
```csharp
Perk,
```

### 1.2 Create `PerkPanelModel`

**New file:** `Assets/Game/Core/Models/Inventory/PerkPanelModel.cs`

Mirrors `BackpackPanelModel` (`Assets/Game/Core/Models/Inventory/BackpackPanelModel.cs`) but filters for `ItemType.Perk` only. Same events: `ItemsUpdated`, `SelectionUpdated`. Same `NextItem()` method (no `PrevItem()` needed -- single-key forward cycling). Keep `PrevItem()` anyway for potential gamepad use.

### 1.3 Add `PerkPanelModel` to `PlayerState`

**File:** `Assets/Game/Features/Characters/Hero/PlayerState.cs`

Add field + property mirroring `backpackPanelModel`:
```csharp
private PerkPanelModel perkPanelModel;
public PerkPanelModel PerkPanelModel => perkPanelModel;
```
Initialize in constructor: `perkPanelModel = new(inventoryModel);`

### 1.4 Create `ShopDef` ScriptableObject

**New file:** `Assets/Game/Core/Models/Shop/ShopDef.cs`

The project uses Unity Localization package. Use `LocalizedString` for inspector-assignable localized text (same pattern as `InteractableBase.actionText`).

```csharp
using UnityEngine.Localization;

[CreateAssetMenu(menuName = "Defs/ShopDef", fileName = "ShopDef")]
public class ShopDef : ScriptableObject {
    public List<ShopItemEntry> items;
}

[Serializable]
public class ShopItemEntry {
    public string itemId;
    public int price;
    public LocalizedString description;
}
```

Loaded via `Resources.Load<ShopDef>($"Shops/{shopId}")`.
Access description text at runtime: `entry.description.GetLocalizedString()`.

---

## Phase 2 -- Input Changes

### 2.1 Update `InputActions.inputactions`

**File:** `Assets/Game/System/InputActions.inputactions`

Player action map changes:
- **Remove** `PrevItem` action entirely
- **Rename** `NextItem` to `SwitchItem`, rebind to `<Keyboard>/1` (keep gamepad rightShoulder)
- **Add** `SwitchPerk` action (Button), bind to `<Keyboard>/2` (gamepad: leftTrigger or similar)
- **Add** `UsePerk` action (Button), bind to `<Keyboard>/s` (gamepad: rightTrigger or similar)
- `UseItem` stays on `<Keyboard>/a`

After editing, save to regenerate `InputActions.cs`.

### 2.2 Update `PlayerController` input handling

**File:** `Assets/Game/Features/Characters/Hero/PlayerController.cs`

- `CheckInventory()`: Replace `Actions.NextItem`/`Actions.PrevItem` with `Actions.SwitchItem` calling `state.BackpackPanelModel.NextItem()`. Add `Actions.SwitchPerk` calling `state.PerkPanelModel.NextItem()`.
- Add `CheckPerkUse()` method: on `Actions.UsePerk.WasPerformedThisFrame()` call `perkUseService.TryUseSelectedItem()`.
- Call `CheckPerkUse()` from `Update` alongside `CheckItemUse()`.

### 2.3 Update `BackpackPanel` UI navigation

**File:** `Assets/Game/UI/Inventory/BackpackPanel.cs`

Remove `uiActions.Left`/`uiActions.Right` calls to `PrevItem()`/`NextItem()` (lines 33-39) since cycling is now done via Player action map key. Or keep Left/Right for navigating within the opened backpack panel if desired. This is UI-mode navigation so it can stay.

---

## Phase 3 -- Perk Use Service

### 3.1 Create perk `ItemUseService` instance

**File:** `Assets/Game/Features/Characters/Hero/PlayerController.cs`

Add a second `ItemUseService` for perks:
```csharp
private ItemUseService perkUseService;
```

In `InitItemUseService()` (or a new `InitPerkUseService()`):
```csharp
perkUseService = new ItemUseService(state.PerkPanelModel);
perkUseService.Register(new ProtectionMaskStrategy());
perkUseService.Register(new ParrotDeployStrategy(this, parrotPrefab));
```

Remove `ParrotDeployStrategy` registration from `itemUseService`.

### 3.2 Refactor `ItemUseService` constructor

**File:** `Assets/Game/Features/Characters/Hero/ItemUse/ItemUseService.cs`

Currently takes `BackpackPanelModel`. Both `BackpackPanelModel` and `PerkPanelModel` share the same shape (SelectedItem, NextItem, etc.). Extract a small interface so `ItemUseService` can work with either:

**New file:** `Assets/Game/Core/Models/Inventory/ISelectablePanelModel.cs`
```csharp
public interface ISelectablePanelModel {
    InventoryItem SelectedItem { get; }
    event Action<IReadOnlyList<InventoryItem>> ItemsUpdated;
    event BackpackPanelModel.SelectionChangedHandler SelectionUpdated;
}
```

Move the `SelectionChangedHandler` delegate type into the interface (or keep it on `BackpackPanelModel` and reference it). Make both `BackpackPanelModel` and `PerkPanelModel` implement it. Change `ItemUseService` constructor to accept `ISelectablePanelModel`. `SelectedItemPanel` also uses this interface to work with either model.

### 3.3 Create `ProtectionMaskStrategy`

**New file:** `Assets/Game/Features/Characters/Hero/ItemUse/ProtectionMaskStrategy.cs`

Stub implementation of `IItemUseStrategy`:
- `ItemId` returns `ItemIds.ProtectionMask`
- `CanUse()` checks inventory count > 0
- `Use()` is a stub (TODO: implement gameplay effect)
- `Update()` empty

### 3.4 Expose `PerkUseService` on `HeroService`

**File:** `Assets/Game/Core/Services/HeroService.cs`

Add `PerkUseService` property, update `Register()`/`Unregister()` to include it.

---

## Phase 4 -- Shop System

### 4.1 Add `DialogActionType.OpenShop`

**File:** `Assets/Game/Core/Models/Dialog/DialogAction.cs`

Add to enum: `OpenShop`

### 4.2 Handle `OpenShop` in `DialogService`

**File:** `Assets/Game/Core/Services/Dialog/DialogService.cs`

Add a `pendingShopId` field. In `ExecuteAction`:
```csharp
case DialogActionType.OpenShop:
    pendingShopId = action.stringParam;
    break;
```

In `EndDialog()`, after cleanup, if `pendingShopId != null`, start a coroutine that waits one frame (for dialog panel close transition to begin), then opens the shop via `G.Menu.OpenMenu(G.Config.ShopPanel)` and calls `LoadShop(shopId)` on the result.

### 4.3 Add `ShopPanel` to `MainConfig`

**File:** `Assets/Game/Configs/MainConfig.cs`

Add under `[Header("In-Game UI")]`:
```csharp
public MenuWindow ShopPanel;
```

### 4.4 Implement `ShopInventory` window

**File:** `Assets/Game/UI/ShopInventory/ShopInventory.cs`

Flesh out the stub:
- Add `[SerializeField] Transform itemContainer` for spawned ShopItem instances
- Add `[SerializeField] LocalizedString notEnoughCoinsText` for feedback message
- `LoadShop(string shopId)` -- loads `ShopDef` from Resources, populates items
- `PopulateItems()` -- instantiates `ShopItem` prefabs into container
- `Update()` -- handles UI navigation (Up/Down to move selection, Select to buy, Cancel to close)
- `TryBuyItem(ShopItemEntry)` -- checks coin count, deducts coins, adds item to inventory, updates UI
- Update description text on selection change
- **Purchased items are not displayed** (skip during `PopulateItems` or destroy on buy)
- **Unaffordable items**: icon 50% transparent, price text red
- Refresh visual states when coin count changes (subscribe to `InventoryModel.OnChange`)
- `pausesGame = true`, `closeOnCancel = true` (set on prefab)

### 4.5 Expand `ShopItem`

**File:** `Assets/Game/UI/ShopInventory/ShopItem.cs`

Add:
- `Setup(ShopItemEntry entry)` -- sets icon from `DefsFacade.I.Items.Get(entry.itemId)`, sets price
- Store `ShopItemEntry` reference for parent to read
- Visual state for "can't afford": icon at 50% alpha, price text color red
- `SetAffordable(bool)` method to toggle between normal and unaffordable appearance

---

## Phase 5 -- HUD Perk Panel

### 5.1 Refactor `SelectedItemPanel` to support both items and perks

**File:** `Assets/Game/UI/Inventory/SelectedItemPanel.cs`

Instead of creating a separate `SelectedPerkPanel` class, unify `SelectedItemPanel` so it can be configured for either panel model via an inspector enum:

```csharp
public enum SelectedPanelMode {
    Item,
    Perk,
}
```

Add `[SerializeField] private SelectedPanelMode mode;` field. In `Awake()`, pick the model and use service based on mode:
- `Item` -> `BackpackPanelModel` + `G.Hero.ItemUseService`
- `Perk` -> `PerkPanelModel` + `G.Hero.PerkUseService`

Both `BackpackPanelModel` and `PerkPanelModel` already implement `ISelectablePanelModel` and share the same event signatures, so the subscription code stays the same. Store the model as `ISelectablePanelModel` and the use service as `ItemUseService`.

In Unity Editor: duplicate the existing `SelectedItemPanel` prefab instance in the HUD, set the copy's mode to `Perk`, position it next to the original.

---

## Phase 6 -- Dialog & Item Data

### 6.1 Update `rikko.json`

**File:** `Assets/Game/Resources/Dialogs/rikko.json`

- Add a "shop" choice in the greeting node: `{ "textKey": "dialog.rikko.greeting.choice_shop", "nextNodeId": "open_shop" }`
- Add `open_shop` node with a line ("Take a look!") and an auto-continue choice with `OpenShop` action (`stringParam: "rikko"`)
- Remove the inline sword purchase from the `offer` node (or remove the `offer` node entirely if it only existed for the sword)

### 6.2 Localization keys needed

The project uses Unity Localization package with string tables. Dialog keys go in the "Dialogs" table (resolved via `G.Strings`). Shop UI keys go in the "UI" table (referenced via `LocalizedString` on `ShopDef` and `ShopInventory`).

**Dialogs table:**
- `dialog.rikko.greeting.choice_shop` -- "See what's for sale"
- `dialog.rikko.open_shop.line_01` -- "Here's what I've got!"

**UI table (referenced via `LocalizedString` fields on ShopDef / ShopInventory):**
- Item descriptions -- set per-entry on the `ShopDef` asset via `LocalizedString` inspector field
- `shop.not_enough_coins` -- "Not enough coins!"

---

## Unity Editor Steps (manual)

1. **InventoryItemsDef asset**: Add `ProtectionMask` entry (type: `Perk`, set icon sprite). Change existing `Parrot` entry type from `Usable` to `Perk`. Save to regenerate `ItemIds.cs`.
2. **InputActions asset**: Open in Input Actions editor, apply changes from Phase 2.1, save to regenerate `InputActions.cs`.
3. **Create ShopDef asset**: `Assets/Game/Resources/Shops/rikko.asset` -- add 3 entries: Sword (10), ProtectionMask (25), Parrot (50).
4. **MainConfig**: Assign `ShopInventory` prefab to the new `ShopPanel` field.
5. **ShopInventory prefab**: Wire up `itemContainer` reference, ensure TweenGroup has "show"/"hide" presets.
6. **HUD perk panel**: Duplicate the existing `SelectedItemPanel` instance in the HUD scene, set the copy's `mode` to `Perk`, position next to the original.
7. **Localization**: Add string table entries listed in Phase 6.2.

---

## Files Summary

**New files (5):**
| File | Purpose |
|------|---------|
| `Assets/Game/Core/Models/Inventory/PerkPanelModel.cs` | Perk selection model (mirrors BackpackPanelModel) |
| `Assets/Game/Core/Models/Inventory/ISelectablePanelModel.cs` | Interface shared by both panel models |
| `Assets/Game/Core/Models/Shop/ShopDef.cs` | ScriptableObject defining shop contents |
| `Assets/Game/Features/Characters/Hero/ItemUse/ProtectionMaskStrategy.cs` | Stub perk strategy |
| `Assets/Docs/Planning/ShopSystem.md` | This plan |

**Modified files (13):**
| File | Change |
|------|--------|
| `Assets/Game/Core/Models/Inventory/InventoryItemsDef.cs` | Add `Perk` to `ItemType` enum |
| `Assets/Game/Core/Models/Inventory/BackpackPanelModel.cs` | Implement `ISelectablePanelModel` |
| `Assets/Game/Features/Characters/Hero/PlayerState.cs` | Add `PerkPanelModel` field |
| `Assets/Game/Features/Characters/Hero/ItemUse/ItemUseService.cs` | Accept `ISelectablePanelModel` |
| `Assets/Game/Features/Characters/Hero/PlayerController.cs` | Add perkUseService, new input checks |
| `Assets/Game/Core/Services/HeroService.cs` | Expose `PerkUseService` |
| `Assets/Game/Core/Models/Dialog/DialogAction.cs` | Add `OpenShop` action type |
| `Assets/Game/Core/Services/Dialog/DialogService.cs` | Handle `OpenShop`, schedule shop opening |
| `Assets/Game/Configs/MainConfig.cs` | Add `ShopPanel` field |
| `Assets/Game/UI/ShopInventory/ShopInventory.cs` | Full implementation |
| `Assets/Game/UI/ShopInventory/ShopItem.cs` | Add `Setup()`, store entry ref, affordability visuals |
| `Assets/Game/UI/Inventory/SelectedItemPanel.cs` | Refactor to support both Item and Perk modes via enum |
| `Assets/Game/System/InputActions.inputactions` | Add UsePerk/SwitchPerk, remove PrevItem, rename NextItem |
| `Assets/Game/Resources/Dialogs/rikko.json` | Add shop node, remove inline sword purchase |

---

## Verification

1. **Compile**: Project builds with no errors after all code changes
2. **Input**: Press 1 to cycle items, 2 to cycle perks, A to use item, S to use perk
3. **Dialog flow**: Interact with Rikko -> greeting -> choose "See what's for sale" -> line plays -> shop UI opens
4. **Shop purchase**: Select sword (10 coins) -> buy -> coins deducted, sword appears in inventory. Select mask (25 coins) -> buy -> mask appears in perk panel. Select parrot (50 coins) -> buy -> parrot in perk panel.
5. **Not enough coins**: Attempt to buy with insufficient coins -> feedback shown, no transaction
6. **Already purchased**: Purchased items disappear from the shop list after buying
7. **Perk HUD**: SelectedPerkPanel shows currently selected perk with icon and cooldown
8. **Perk activation**: Press S to activate selected perk (parrot deploys, mask is stub)
9. **Persistence**: Save/load preserves purchased items (they live in InventoryModel which is already serialized)