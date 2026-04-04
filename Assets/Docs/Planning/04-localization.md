# 03 - Localization

## Decision

Use the Unity Localization package as the localization backend.

Do not build a full custom localization library.

Recommended shape for this project:

- Unity Localization package for string tables, locale management, editor tooling, import/export, pseudo-localization, and localized assets.
- Keep the existing custom gameplay-side systems where they already add value:
  - `DialogService` and dialog graph traversal stay custom.
  - `G.Locale` stays as the project-facing locale entry point, but becomes a thin wrapper around Unity Localization.

This is the more common and more convenient approach for a Unity project of this size.

Why:

- Unity already provides string localization, asset localization, pseudo-localization, and import/export to XLIFF, CSV, and Google Sheets in the Localization package.
- The package is supported for the current editor line. This project is on Unity `2022.3.62f2`, and Unity's 2022.3 manual currently lists `com.unity.localization@1.5.x` as released/available for 2022.3.
- You already have a custom locale switcher and a custom dialog runtime. Replacing those systems entirely would create unnecessary integration risk. Using the package as the backend keeps the migration small and local.

Official references:

- Unity 2022.3 manual: https://docs.unity3d.com/2022.3/Documentation/Manual/com.unity.localization.html
- Localization package manual 1.5: https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/index.html
- String Tables: https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/StringTables.html
- Quick Start: https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/QuickStartGuide.html
- Scripting: https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/Scripting.html

## Why Not a Fully Custom Library

A custom library would be simpler only for the current dialog JSON use case.

It becomes worse once all of these are included:

- menu labels
- button text
- locale switching in options
- in-world signs and boards
- item names and descriptions
- possible future asset localization
- translation workflow and import/export

At that point you would be rebuilding:

- string tables
- locale asset management
- editor tooling
- runtime locale propagation
- translator-friendly import/export
- pseudo-localization testing

That is avoidable work with long-term maintenance cost.

## Current State

The project already has:

- `LocaleService` under `Assets/Game/Core/Services/Locale/LocaleService.cs`
- `G.Locale` bootstrap integration
- options menu locale switching via `LocalePicker`
- custom dialog loading from locale-specific JSON under `Assets/Game/Resources/Locale/{locale}/Dialogs/`

Current issues:

- localization is partial and custom
- menus are mostly hardcoded in prefabs/components
- dialogs currently embed English strings directly in JSON
- `WoodenBoardBig.prefab` contains hardcoded text content
- item definitions currently only store IDs and icons, not localized names/descriptions

## Proposed Architecture

### 1. Use Unity Localization as the Single String Source

Create Localization tables under a project-owned folder, for example:

`Assets/Game/Localization/`

Suggested table collections:

- `UI` — menu labels, button text, options screen
- `Dialogs` — speaker names, dialog lines, choice text

Additional collections to create when needed (deferred):

- `Items` — item names and descriptions (Phase 6)
- `World` — signs, boards, in-world text (Phase 5)

This keeps all translations centralized and editor-managed.

### 2. Keep `G.Locale` as a Thin Facade

Do not remove `LocaleService`.

Instead, refactor it so:

- `CurrentLocale` mirrors Unity Localization's selected locale
- `SetLocale(localeCode)` sets Unity Localization's selected locale
- `OnLocaleChanged` continues to exist for project systems that already listen to it

This avoids a broad rewrite of menu and service code that already depends on `G.Locale`.

### 3. Keep Dialog Graphs Custom, Move Text Out of Dialog JSON

Do not replace the dialog graph system with Unity's package.

Keep:

- `DialogDef`
- `DialogNode`
- `DialogService`
- conditions/actions/choices flow

Change dialog content files from locale-specific text files to locale-agnostic graph files.

Example rename:

- `rikko.en.json` -> `rikko.dialog.json`

The new dialog JSON should store string keys, not translated text.

Speaker name localization uses convention-based key derivation:

- `dialog.{dialogId}.speaker.{speaker}` resolves to the localized display name
- no explicit speaker registry needed in the JSON — the runtime derives the key from `dialogId` + `speaker`

This avoids maintaining a redundant mapping in every dialog file.

Recommended shape:

```json
{
  "dialogId": "rikko",
  "entryNodeId": "greeting",
  "nodes": [
    {
      "nodeId": "greeting",
      "speaker": "rikko",
      "lines": [
        { "textKey": "dialog.rikko.greeting.line_01", "soundId": "speech1" },
        { "textKey": "dialog.rikko.greeting.line_02" }
      ],
      "choices": [
        { "textKey": "dialog.rikko.greeting.choice_01", "nextNodeId": "offer" },
        { "textKey": "dialog.rikko.greeting.choice_02" }
      ]
    }
  ]
}
```

Note: `soundId` on lines is optional and corresponds to the existing `DialogLine.soundId` field used by the dialog sound system.

At runtime:

- `DialogLoader` loads `rikko.dialog.json`
- `DialogService` resolves the speaker display name via convention: `dialog.{dialogId}.speaker.{speaker}`
- `DialogService` resolves line `textKey` and choice `textKey` through a localization resolver interface
- `DialogPanel` continues to receive already-resolved strings and remains presentation-only

This is the safest hybrid model because it preserves current dialog behavior and only changes the text source.

### 3a. Decouple DialogService from Unity Localization via IStringResolver

`DialogService` should not reference `UnityEngine.Localization` directly. Instead, introduce a small interface:

```csharp
public interface IStringResolver {
    string Resolve(string key);
}
```

- `UnityStringResolver` implements this using Unity Localization string tables.
- `DialogService` receives `IStringResolver` and uses it to resolve `textKey` values and convention-based speaker name keys.
- This keeps `DialogService` testable and decoupled from the localization backend.

### 4. Localize Menus Using Unity's Built-In LocalizeStringEvent

Use `LocalizeStringEvent` — the component Unity Localization already provides for localizing `TMP_Text`.

No custom `LocalizedTmpText` or `LocalizedMenuButtonText` components are needed.

Recommended approach:

- Add a `LocalizeStringEvent` component to each `TMP_Text` that needs localization.
- In the Inspector, set the Table Collection and Entry for each component.
- `LocalizeStringEvent` automatically updates the text when the locale changes.

For `MenuButton` labels:

- Add `LocalizeStringEvent` to the child `TMP_Text` inside each `MenuButton` prefab.
- No changes to `MenuButton.cs` are required.

Why:

- zero custom code for static UI text
- small prefab diffs
- no risk to button visuals, selection, or animation logic
- easy to retrofit across main menu, pause menu, options menu
- uses Unity's maintained component instead of a custom wrapper

### 5. Localize In-World Text by Key, Not Raw Text

For things like signs and boards:

- do not keep literal text in prefabs or gameplay scripts
- store a localization key in the gameplay source object

Example:

- `InfoSign.message` becomes `messageKey`
- `InfoSign.ShowInfo()` resolves the localized string for `messageKey`
- `WoodenBoardBig` remains a display component and receives final text via `SetText()`

This keeps `WoodenBoardBig` generic and avoids coupling it to localization internals.

### 6. Add Localized Item Metadata to Definitions

Current `ItemDef` contains:

- `id`
- `icon`
- `type`
- `cooldown`

Add optional localization keys:

- `displayNameKey`
- `descriptionKey`

Suggested usage:

- inventory/tooltips resolve `displayNameKey`
- future shop/tooltip/detail panels resolve `descriptionKey`

Do not localize by item ID directly in UI code. Keep the mapping explicit in item definitions.

## Table and Key Structure

Use stable namespaced keys.

Suggested conventions:

- `ui.main_menu.title`
- `ui.main_menu.continue`
- `ui.options.back`
- `ui.options.language`
- `dialog.rikko.speaker.rikko`
- `dialog.rikko.speaker.hero`
- `dialog.rikko.greeting.line_01`
- `dialog.rikko.greeting.choice_01`
- `world.sign.harbor_warning`
- `item.sword.name`
- `item.sword.description`

Rules:

- keys should be stable and not depend on English text
- use lowercase with dots
- never use the visible text itself as the key

## Runtime Loading Strategy

The biggest practical concern is runtime string lookup.

Important note from Unity's String Tables docs:

- string tables can be configured for preloading
- without preload, the first request may require an async load before text is immediately available

Recommendation:

- preload `UI` and `Dialogs` tables for the active locale
- preload `Items` and `World` tables later when those phases are implemented

This keeps menu opening and dialog start deterministic and avoids async timing glitches in the typewriter flow.

Do not redesign the dialog UI around asynchronous text loading if preloading is sufficient.

## Migration Plan

### Phase 1 - Package Setup

1. Add `com.unity.localization` to `Packages/manifest.json`.
2. Create Localization Settings and supported locales.
3. Create table collections: `UI` and `Dialogs`.
4. Configure preload for both table collections.

### Phase 2 - Locale Service Integration

1. Refactor `LocaleService` to proxy Unity Localization's selected locale.
2. Keep `G.Locale.CurrentLocale` and `OnLocaleChanged`.
3. Keep the options menu integration unchanged at call sites.
4. Remove custom dialog cache invalidation logic only after dialog loading no longer depends on locale-specific filenames.

### Phase 3 - Menu Localization

1. Add `LocalizeStringEvent` components to `TMP_Text` objects on:
   - main menu
   - pause menu
   - options menu
   - any reusable menu widgets with visible labels
2. Assign table collection (`UI`) and entry key for each component in the Inspector.
3. No custom C# components needed — this is purely Inspector/prefab work.

### Phase 4 - Dialog Localization

1. Change dialog data classes from literal strings to string keys:
   - keep `speaker` field as-is (speaker name resolved by convention at runtime)
   - line `text` -> `textKey`
   - choice `text` -> `textKey`
   - keep `soundId` on lines (already present in `DialogLine`)
2. Rename dialog files from locale-specific text files to locale-agnostic graph files:
   - `rikko.en.json` -> `rikko.dialog.json`
3. Update `DialogLoader` so it loads locale-agnostic graph files.
4. Introduce a small `IStringResolver` interface for localization lookups. `DialogService` uses it to resolve speaker names (by convention key), line `textKey`, and choice `textKey` before emitting `DialogViewState`. The Unity Localization implementation lives behind this interface — `DialogService` never references `UnityEngine.Localization` directly.
5. Keep `DialogPanel` free of localization lookups.

### Phase 5 - World Text Localization (deferred)

Can be done later when world text content grows beyond a few signs.

1. Replace raw sign/board text fields with localization keys.
2. Update `InfoSign` and similar world interactables to resolve text through Unity Localization.
3. Keep `WoodenBoardBig` as a pure display view.

### Phase 6 - Item Localization (deferred)

Can be done later when item tooltips or shop UI are implemented.

1. Extend `ItemDef` with localization keys for name/description.
2. Add item entries to the `Items` table collection.
3. Update any UI that shows item names or descriptions to resolve through those keys.

### Phase 7 - Cleanup

1. Remove old `Resources/Locale/{locale}/...` dialog text structure after migration.
2. Keep only locale-agnostic graph files plus Unity Localization tables.
3. Update documentation and authoring workflow.

## Expected Code Changes

Likely touched files:

- `Packages/manifest.json`
- `Assets/Game/Core/Services/Locale/LocaleService.cs`
- `Assets/Game/Core/Models/Dialog/*`
- `Assets/Game/Core/Services/Dialog/DialogService.cs`
- `Assets/Game/Core/Models/Dialog/DialogLoader.cs`
- `Assets/Game/Features/Interactive/InfoSign/InfoSign.cs` (deferred, Phase 5)
- `Assets/Game/UI/WoodenDialog/WoodenBoardBig.cs` (deferred, Phase 5)
- `Assets/Game/Core/Models/Inventory/InventoryItemsDef.cs` (deferred, Phase 6)

Likely new files:

- `Assets/Game/Core/Services/Locale/IStringResolver.cs` — thin interface for string key resolution
- `Assets/Game/Core/Services/Locale/UnityStringResolver.cs` — implementation backed by Unity Localization string tables
- Localization tables/assets under `Assets/Game/Localization/`

## Authoring Workflow

### Menu/UI

- Designers add `LocalizeStringEvent` components to `TMP_Text` objects and assign table/entry in the Inspector.

### Dialogs

- Writers edit dialog graph JSON with stable keys.
- Translators work in Unity string tables, CSV, XLIFF, or Google Sheets export/import.

### World Signs / Boards

- Designers assign a `messageKey` instead of writing literal text into the prefab/script.

### Items

- Designers assign `displayNameKey` and `descriptionKey` on the item definition asset.

## Risks

### Dialog Migration Risk

Changing dialogs from literal text to keys touches parser/data/runtime together.

Mitigation:

- migrate one dialog first (`rikko`)
- verify end-to-end before bulk migration

### UI Prefab Churn

Applying localization components to many prefabs can create noisy diffs.

Mitigation:

- start with main menu, pause menu, options menu
- use small reusable components

### Text Overflow / Pixel Presentation

Localized text may be longer than English and can break layout.

Mitigation:

- run pseudo-localization early
- verify TMP bounds, line breaks, and pixel readability
- export character sets from string tables for TMP font atlas updates if additional languages are added

### Async Loading Surprises

String tables may not be immediately available without preload.

Mitigation:

- preload required tables for the active locale
- avoid ad hoc async lookups in hot UI paths unless necessary

## Recommendation Summary

Choose Unity Localization package, not a full custom localization library.

For this project, the best implementation is hybrid:

- Unity Localization owns translated strings and locale assets.
- Existing game systems keep their runtime behavior.
- Dialog JSON becomes locale-agnostic and references string keys.
- Menus, items, and world signs resolve text from shared string tables.

This is the most common Unity approach, the most maintainable long-term approach, and the lowest-risk way to introduce localization without destabilizing gameplay code.
