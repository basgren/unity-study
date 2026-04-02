# Dialog System Plan (v1)

## Goal
Add a node-based dialog system for NPC conversations with branching choices, sequential lines per speaker, conditions (item checks, flags), and actions (give/take items, set flags).

This plan targets:
- data-driven dialog trees stored as JSON files (easy to edit, diff, and localize)
- multiple sequential lines per node before choices appear
- player choices that branch to different nodes
- conditions that show/hide choices (item possession, flags, custom)
- actions that fire when a choice is picked or a node is entered
- dialog UI opened through MenuManager as a TweenWindow
- i18n-ready: text separated from structure so locale files can be swapped

No voice acting, no timeline/cutscene integration in v1.

## Data Format: JSON

### Why JSON
- **Easy to edit** — any text editor, no Unity Inspector required
- **Clean diffs** — readable git history for dialog changes
- **i18n-native** — each locale is a complete, self-contained set of dialog files
- **Bulk editable** — search-replace across all dialogs, scripted generation, external tools
- Custom parser handles enum strings (`"HasItem"` not `0`) for readability

### File layout — one directory per locale

Each locale has its own complete copy of every dialog file. Files are self-contained — a translator can open any single file and see full readable dialog without cross-referencing.

```
Assets/Game/Resources/Locale/
    en/
        merchant.en.json
        old_sailor.en.json
        parrot_lady.en.json
    ru/
        merchant.ru.json
        old_sailor.ru.json
        parrot_lady.ru.json
```

Loaded at runtime via `Resources.Load<TextAsset>($"Locale/{locale}/{dialogId}.{locale}")`. The active locale is managed by `LocaleService` (see [Locale Service section](#locale-service) below).

### JSON schema

```json
{
  "dialogId": "merchant",
  "entryNodeId": "greeting",
  "nodes": [
    {
      "nodeId": "greeting",
      "speaker": "merchant",
      "lines": [
        "Ahoy, traveler!",
        "Looking for something sharp?"
      ],
      "choices": [
        {
          "text": "Show me what you have",
          "nextNodeId": "offer"
        },
        {
          "text": "Not today",
          "nextNodeId": null
        }
      ],
      "onEnterActions": []
    },
    {
      "nodeId": "offer",
      "speaker": "merchant",
      "lines": ["I've got a fine sword. 10 coins."],
      "choices": [
        {
          "text": "I'll take it",
          "nextNodeId": "buy_sword",
          "conditions": [{ "type": "HasItem", "stringParam": "coin", "intParam": 10 }],
          "actions": [
            { "type": "RemoveItem", "stringParam": "coin", "intParam": 10 },
            { "type": "GiveItem", "stringParam": "sword", "intParam": 1 }
          ]
        },
        {
          "text": "I'll take it",
          "nextNodeId": "no_coins",
          "conditions": [{ "type": "DoesNotHaveItem", "stringParam": "coin" }]
        },
        {
          "text": "No thanks",
          "nextNodeId": "farewell"
        }
      ]
    }
  ]
}
```

**Rules:**
- `lines` are displayed one by one; the player presses "next" to advance.
- After the last line, `choices` appear. Empty `choices` array = dialog ends automatically.
- A single choice with empty `text` acts as auto-continue (no player input needed).
- `nextNodeId: null` or omitted = end dialog after this choice.
- `conditions` and `actions` can be omitted (default to empty arrays).

## C# Data Model

Plain C# classes deserialized from JSON. No `[SerializeField]`, no ScriptableObject.

### DialogDef — one per conversation

```csharp
[Serializable]
public class DialogDef {
    public string dialogId;
    public string entryNodeId;
    public DialogNode[] nodes;
}
```

### DialogNode

```csharp
[Serializable]
public class DialogNode {
    public string nodeId;
    public string speaker;            // display name shown in UI
    public string[] lines;            // sequential lines shown one at a time
    public DialogChoice[] choices;    // shown after last line; empty = dialog ends
    public DialogAction[] onEnterActions; // fire when this node is entered
}
```

### DialogChoice

```csharp
[Serializable]
public class DialogChoice {
    public string text;               // what the player sees (empty = auto-continue)
    public string nextNodeId;         // which node to go to (null = end dialog)
    public DialogCondition[] conditions;
    public DialogAction[] actions;
}
```

### DialogCondition (enum-based)

```csharp
public enum ConditionType {
    HasItem,          // player has >= intParam of stringParam item
    DoesNotHaveItem,  // player has 0 of stringParam item
    FlagSet,          // stringParam flag is set
    FlagNotSet,       // stringParam flag is not set
}

[Serializable]
public class DialogCondition {
    public ConditionType type;
    public string stringParam;   // item id or flag name
    public int intParam;         // minimum count (for HasItem, default 1)
}
```

Evaluation logic:
```
HasItem         → inventory.GetCount(stringParam) >= max(intParam, 1)
DoesNotHaveItem → inventory.GetCount(stringParam) == 0
FlagSet         → playerState.flags.Contains(stringParam)
FlagNotSet      → !playerState.flags.Contains(stringParam)
```

### DialogAction (enum-based)

```csharp
public enum DialogActionType {
    GiveItem,      // add intParam of stringParam item to inventory
    RemoveItem,    // remove intParam of stringParam item from inventory
    SetFlag,       // set stringParam flag
    ClearFlag,     // clear stringParam flag
}

[Serializable]
public class DialogAction {
    public DialogActionType type;
    public string stringParam;
    public int intParam;
}
```

For one-off custom behavior (spawn enemy, start quest, play sound), the NPC script can subscribe to a dialog event or use a UnityEvent wired in the Inspector on the NPC interactable.

### JSON Parsing — custom parser with enum support

`JsonUtility` does not support string enum values. Instead of writing `0` in JSON (unreadable), use a small custom parser built on top of `JsonUtility` or manual parsing:

```csharp
public static class DialogParser {
    public static DialogDef Parse(string json) {
        // 1. Parse JSON into DialogDef using JsonUtility (enums will be 0)
        // 2. Walk the raw JSON tokens to resolve enum string fields:
        //    "HasItem" → ConditionType.HasItem
        //    "GiveItem" → DialogActionType.GiveItem
        // Or: use a simple token-based approach / mini JSON reader
    }
}
```

This keeps JSON files human-readable (`"type": "HasItem"` not `"type": 0`) without pulling in Newtonsoft. The parser is small — it only needs to handle `ConditionType` and `DialogActionType` enums by name.

### Dialog loading

```csharp
public static class DialogLoader {
    private static readonly Dictionary<string, DialogDef> cache = new();

    public static DialogDef Load(string dialogId) {
        if (cache.TryGetValue(dialogId, out var cached)) {
            return cached;
        }

        var locale = G.Locale.CurrentLocale;
        var textAsset = Resources.Load<TextAsset>($"Locale/{locale}/{dialogId}.{locale}");
        var def = DialogParser.Parse(textAsset.text);
        cache[dialogId] = def;
        return def;
    }

    /// <summary>
    /// Called by LocaleService when locale changes. Forces reload on next access.
    /// </summary>
    public static void ClearCache() {
        cache.Clear();
    }
}
```

Loaded on demand when an NPC starts a conversation. Cache is cleared when locale switches.

## Locale Service

Centralized locale management. Registered as `G.Locale`. Allows switching language at runtime from the options menu without reloading the game.

```csharp
public sealed class LocaleService : MonoBehaviour {
    private const string DefaultLocale = "en";

    public string CurrentLocale { get; private set; }

    public event Action<string> OnLocaleChanged;

    public void Init() {
        CurrentLocale = G.Settings.Current.Locale ?? DefaultLocale;
    }

    public void SetLocale(string locale) {
        if (CurrentLocale == locale) {
            return;
        }

        CurrentLocale = locale;
        G.Settings.Current.Locale = locale;
        G.Settings.Save();

        // Clear any cached localized data so it reloads from the new locale
        DialogLoader.ClearCache();

        OnLocaleChanged?.Invoke(locale);
    }
}
```

Locale is stored as a new `Locale` field on `GameSettings` (alongside `MusicVolume`/`SfxVolume`) and persisted via the existing `G.Settings.Save()` JSON file.

**Key design points:**
- Persisted via `GameSettings` / `SettingsService` — same file as audio settings, no separate storage
- `SetLocale()` clears dialog cache so next dialog load picks up the new language
- `OnLocaleChanged` event lets any UI that displays localized text refresh itself
- No game reload needed — active dialogs would need to be re-opened, but that's fine since locale is changed from the options menu (no dialog is active)
- Other systems that need localized text (UI labels, item names, etc.) can also subscribe to `OnLocaleChanged` and re-read from their locale-specific sources

### Adding a new language
1. Create directory `Resources/Locale/{code}/`
2. Copy all dialog JSON files from `en/`, rename suffix to `.{code}.json`
3. Translate all `speaker`, `lines`, and `choices.text` values
4. Structure, node IDs, conditions, and actions remain identical
5. Add the locale code to the language picker in the options menu

## Player Flags

Add a simple string-based flag set to `PlayerState`:

```csharp
// In PlayerState.cs
[SerializeField]
private List<string> flags = new();

public bool HasFlag(string flag) => flags.Contains(flag);
public void SetFlag(string flag) { if (!flags.Contains(flag)) flags.Add(flag); }
public void ClearFlag(string flag) => flags.Remove(flag);
```

Using `List<string>` with `[SerializeField]` instead of `HashSet` so Unity can serialize it for save/load. The list will be small (tens of flags at most), so linear search is fine.

## Runtime

### DialogService

Registered as `G.Dialog`. Owns the runtime state of the active conversation.

```csharp
public sealed class DialogService : MonoBehaviour {
    // Events the UI subscribes to
    public event Action<DialogLineEvent> OnLineChanged;
    public event Action<List<DialogChoice>> OnChoicesShown;
    public event Action OnDialogEnded;

    public bool IsActive { get; }

    public void StartDialog(DialogDef dialog);
    public void AdvanceLine();                  // next line or show choices
    public void PickChoice(int choiceIndex);    // execute choice
    public void EndDialog();                    // force-close
}
```

**State:** current `DialogDef`, current `DialogNode`, current line index, filtered visible choices.

**Flow:**
1. `StartDialog(def)` → enter node `entryNodeId` → fire `onEnterActions` → emit first line
2. `AdvanceLine()` → increment line index; if more lines, emit next line; if last line, evaluate conditions and emit visible choices
3. `PickChoice(i)` → fire choice actions → if `nextNodeId` is set, enter that node; otherwise end dialog
4. `EndDialog()` → clean up, emit `OnDialogEnded`

### DialogLineEvent

```csharp
public struct DialogLineEvent {
    public string Speaker;
    public string Text;
    public bool IsLastLine;  // UI can show "next" vs prepare for choices
}
```

### Condition & Action Evaluation

Static helpers (or methods on DialogService):

```csharp
public static bool EvaluateCondition(DialogCondition cond, PlayerState state);
public static void ExecuteAction(DialogAction action, PlayerState state);
```

These read `G.Game.playerState.inventory` and `G.Game.playerState.flags`.

## UI

### DialogPanel (TweenWindow)

Opened through `G.Menu.OpenMenu(dialogPanelPrefab)`. Subscribes to `G.Dialog` events.

**Layout:**
```
+--------------------------------------+
|  Speaker Name                        |
|                                      |
|  Dialog text line here...            |
|                                      |
|  > Choice 1                          |
|  > Choice 2                          |
|  > Choice 3                          |
+--------------------------------------+
```

- Speaker name at the top
- Text area for the current line
- Choice buttons shown only after the last line; hidden during sequential lines
- "Next" indicator (arrow / prompt) shown while advancing through lines
- Pressing Interact/Confirm advances lines; pressing a choice button picks it

**Behavior:**
- `closeOnCancel = false` — player cannot ESC out mid-conversation (or optionally true for skippable dialogs)
- `pausesGame = true` — default, configurable per dialog or panel

### DialogNPC (InteractableBase)

```csharp
public class DialogNPC : InteractableBase {
    [SerializeField] private string dialogId;   // matches JSON filename, e.g. "merchant"

    protected override void DoInteract() {
        G.Dialog.StartDialog(dialogId);
    }
}
```

Simple. The NPC just references a dialog ID (JSON filename). DialogService loads and runs it.

For NPCs that need to change dialog based on game state (e.g., different dialog after a quest), the NPC script can pick the right ID:

```csharp
[SerializeField] private string defaultDialogId;
[SerializeField] private string afterQuestDialogId;
[SerializeField] private string questCompleteFlag;

protected override void DoInteract() {
    var id = G.Game.playerState.HasFlag(questCompleteFlag)
        ? afterQuestDialogId
        : defaultDialogId;
    G.Dialog.StartDialog(id);
}
```

## File Placement

```
Assets/Game/Core/Models/Dialog/
    DialogDef.cs            (plain C# class)
    DialogNode.cs
    DialogChoice.cs
    DialogCondition.cs
    DialogAction.cs
    DialogParser.cs         (JSON → C# with enum string support)
    DialogLoader.cs         (locale-aware loading + caching)

Assets/Game/Core/Services/Dialog/
    DialogService.cs

Assets/Game/Core/Services/Locale/
    LocaleService.cs        (centralized locale switching)

Assets/Game/UI/Dialog/
    DialogPanel.cs          (TweenWindow)
    DialogPanel.prefab
    DialogChoiceBtn.cs      (choice button controller)

Assets/Game/Features/Interactive/DialogNPC/
    DialogNPC.cs            (InteractableBase)

Assets/Game/Resources/Locale/
    en/
        merchant.en.json
        old_sailor.en.json
        ...
    ru/
        merchant.ru.json
        old_sailor.ru.json
        ...
```

Bootstrap: add `G.Dialog` and `G.Locale` to `G.cs` and create in `GInit`.

## Example: merchant.json

```json
{
  "dialogId": "merchant",
  "entryNodeId": "greeting",
  "nodes": [
    {
      "nodeId": "greeting",
      "speaker": "merchant",
      "lines": [
        "Ahoy, traveler!",
        "Looking for something sharp?"
      ],
      "choices": [
        { "text": "Show me what you have", "nextNodeId": "offer" },
        { "text": "Not today" }
      ]
    },
    {
      "nodeId": "offer",
      "speaker": "merchant",
      "lines": ["I've got a fine sword. 10 coins."],
      "choices": [
        {
          "text": "I'll take it",
          "nextNodeId": "buy_sword",
          "conditions": [{ "type": "HasItem", "stringParam": "coin", "intParam": 10 }],
          "actions": [
            { "type": "RemoveItem", "stringParam": "coin", "intParam": 10 },
            { "type": "GiveItem", "stringParam": "sword", "intParam": 1 }
          ]
        },
        {
          "text": "I'll take it",
          "nextNodeId": "no_coins",
          "conditions": [{ "type": "DoesNotHaveItem", "stringParam": "coin" }]
        },
        { "text": "No thanks", "nextNodeId": "farewell" }
      ]
    },
    {
      "nodeId": "buy_sword",
      "speaker": "merchant",
      "lines": ["Pleasure doing business!"],
      "onEnterActions": [{ "type": "SetFlag", "stringParam": "bought_sword" }],
      "choices": []
    },
    {
      "nodeId": "no_coins",
      "speaker": "merchant",
      "lines": ["Come back when you've got the coins, matey."],
      "choices": []
    },
    {
      "nodeId": "farewell",
      "speaker": "merchant",
      "lines": ["Suit yourself."],
      "choices": []
    }
  ]
}
```

Note: two "I'll take it" choices with different conditions — only one will be visible depending on inventory state. Empty `choices` array = dialog ends after last line.

## Sequence Diagram

```
Player        DialogNPC      DialogService     DialogPanel      MenuManager
  |               |               |                |                |
  |--interact---->|               |                |                |
  |               |--StartDialog->|                |                |
  |               |               |--OpenMenu------|--------------->|
  |               |               |--OnLineChanged>|                |
  |               |               |                |--show line--   |
  |               |               |                |                |
  |--next---------|-------------->| AdvanceLine()  |                |
  |               |               |--OnLineChanged>|                |
  |               |               |                |--show line--   |
  |               |               |                |                |
  |--next---------|-------------->| AdvanceLine()  |                |
  |               |               |--OnChoicesShown|                |
  |               |               |                |--show btns--   |
  |               |               |                |                |
  |--pick choice--|-------------->| PickChoice(1)  |                |
  |               |               |--exec actions  |                |
  |               |               |--OnLineChanged>| (next node)    |
  |               |               |    ...         |                |
  |               |               |--OnDialogEnded>|                |
  |               |               |                |--CloseTop----->|
```

## Implementation Stages

1. **Stage 1 (MVP)**
   - C# data classes + `DialogParser` (custom JSON with string enums) + `DialogLoader`
   - `LocaleService` with `SetLocale()`, `PlayerPrefs` persistence, cache invalidation
   - `DialogCondition` with `HasItem` / `DoesNotHaveItem` / `FlagSet` / `FlagNotSet`
   - `DialogAction` with `GiveItem` / `RemoveItem` / `SetFlag` / `ClearFlag`
   - Player flags on `PlayerState`
   - `DialogService` with start / advance / pick / end
   - `DialogPanel` (TweenWindow) with speaker name, text, choice buttons
   - `DialogNPC` interactable
   - At least one locale directory (`en/`) with example dialog

2. **Stage 2 (Polish)**
   - Language picker in options menu wired to `G.Locale.SetLocale()`
   - Typewriter text effect (reveal characters one by one)
   - Sound effects per line or per speaker

3. **Stage 3 (If needed)**
   - Custom condition/action types via additional enums or callback registration
   - Visual dialog editor (node graph) or external tool export
   - Dialog log / history scroll
   - Integration with timeline/cutscenes

## Important Notes For This Project
- DialogPanel should use `useUnscaledTime = true` for tween transitions since it pauses the game.
- Keep dialog content in JSON files under `Resources/Locale/{code}/`, not hard-coded in NPC scripts.
- Condition/action evaluation must read from `G.Game.playerState` — do not cache stale inventory state.
- Do not add heavyweight packages (Yarn Spinner, Ink, etc.) unless the built-in system proves insufficient.
- The enum-based condition/action approach keeps things simple; extend with new enum values as needs grow.
- Use a custom `DialogParser` for JSON deserialization — it must handle string enum values (`"HasItem"` not `0`).
- All locale directories must have the same set of dialog files — missing files will cause load errors.
- Locale switch clears the dialog cache; any currently open dialog should be closed before switching (enforced by switching from options menu, where no dialog is active).