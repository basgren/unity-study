# 03 — Dialog Sounds

## Goal

Extend the dialog system so that:

1. Each dialog line can optionally specify a `soundId` — an AudioCue played when the line appears.
2. Each character has a default talk sound used when no per-line `soundId` is set.
3. A global fallback sound covers characters without a profile.
4. All dialog AudioCue references live in a centralized, hierarchical ScriptableObject structure.

## Current State

`DialogPanel` already plays talk sounds per speaker via serialized fields on the prefab:

```csharp
// DialogPanel.cs (lines 40-45)
[SerializeField] private AudioCue defaultTalkSound;
[SerializeField] private SpeakerSound[] speakerSounds;  // speaker name → AudioCue
```

`PlayTalkSound(speaker)` looks up the matching cue and calls `G.Audio.Play2D(cue)`.

This works but is not extensible: no per-line overrides, no hierarchy, references buried in a UI prefab.

## Design

### Data Model Change — `DialogLine`

`DialogNode.lines` changes from `string[]` to `DialogLine[]`:

```csharp
// Assets/Game/Core/Models/Dialog/DialogLine.cs  (new file)
[Serializable]
public class DialogLine {
    public string text;
    public string soundId;  // optional, null → use character default
}
```

```csharp
// Assets/Game/Core/Models/Dialog/DialogNode.cs  (modified)
public DialogLine[] lines;   // was: public string[] lines;
```

**JSON format** changes from:

```json
"lines": ["Ahoy, traveler!", "Looking for something sharp?"]
```

to:

```json
"lines": [
    { "text": "Ahoy, traveler!", "soundId": "greeting" },
    { "text": "Looking for something sharp?" }
]
```

`soundId` is omitted when the line should use the character's default sound.

### Sound Library — ScriptableObjects

Two new ScriptableObjects following the existing `PlayerSoundProfile` → `MoveSoundProfile` pattern:

```
Assets/Game/Audio/Dialog/
├── CharacterSoundProfile.cs
└── DialogSoundLibrary.cs
```

**CharacterSoundProfile** — one per character:

```csharp
// Assets/Game/Audio/Dialog/CharacterSoundProfile.cs
[CreateAssetMenu(fileName = "CharacterSoundProfile",
                 menuName = "Audio/Profiles/Dialog/CharacterSoundProfile")]
public class CharacterSoundProfile : ScriptableObject {
    [SerializeField] private AudioCue defaultTalkSound;
    [SerializeField] private SoundEntry[] sounds;

    public AudioCue DefaultTalkSound => defaultTalkSound;

    public AudioCue FindSound(string soundId) {
        if (string.IsNullOrEmpty(soundId) || sounds == null) {
            return null;
        }
        for (int i = 0; i < sounds.Length; i++) {
            if (sounds[i].id == soundId) {
                return sounds[i].cue;
            }
        }
        return null;
    }

    [Serializable]
    public struct SoundEntry {
        public string id;
        public AudioCue cue;
    }
}
```

**DialogSoundLibrary** — top-level registry:

```csharp
// Assets/Game/Audio/Dialog/DialogSoundLibrary.cs
[CreateAssetMenu(fileName = "DialogSoundLibrary",
                 menuName = "Audio/Profiles/Dialog/DialogSoundLibrary")]
public class DialogSoundLibrary : ScriptableObject {
    [SerializeField] private AudioCue globalDefaultTalkSound;
    [SerializeField] private CharacterEntry[] characters;

    /// <summary>
    /// Resolves the AudioCue for a dialog line.
    /// Fallback chain: soundId match → character default → global default.
    /// </summary>
    public AudioCue Resolve(string speaker, string soundId) {
        var profile = FindProfile(speaker);
        if (profile != null) {
            var cue = profile.FindSound(soundId);
            if (cue != null) {
                return cue;
            }
            if (profile.DefaultTalkSound != null) {
                return profile.DefaultTalkSound;
            }
        }
        return globalDefaultTalkSound;
    }

    private CharacterSoundProfile FindProfile(string speaker) {
        if (characters == null || string.IsNullOrEmpty(speaker)) {
            return null;
        }
        for (int i = 0; i < characters.Length; i++) {
            if (string.Equals(characters[i].speaker, speaker,
                              StringComparison.OrdinalIgnoreCase)) {
                return characters[i].profile;
            }
        }
        return null;
    }

    [Serializable]
    public struct CharacterEntry {
        public string speaker;
        public CharacterSoundProfile profile;
    }
}
```

### Asset Hierarchy (Unity Project window)

```
Assets/Game/Audio/Dialog/
├── DialogSoundLibrary.asset          ← single top-level registry
├── Merchant/
│   ├── MerchantSoundProfile.asset    ← CharacterSoundProfile for Merchant
│   ├── MerchantGreeting.asset        ← AudioCue (optional per-line sounds)
│   └── MerchantDefault.asset         ← AudioCue (character default)
├── Guard/
│   ├── GuardSoundProfile.asset
│   └── ...
└── DefaultTalk.asset                 ← AudioCue (global fallback)
```

### Wiring via MainConfig

```csharp
// Assets/Game/Configs/MainConfig.cs  (modified)
[Header("Audio")]
public AudioMixer AudioMixer;
public AudioMixerGroup SfxMixerGroup;
public DialogSoundLibrary DialogSoundLibrary;   // ← new field
```

### Sound Resolution — in DialogService

Sound playback moves from `DialogPanel` to `DialogService.EmitCurrentLine()`:

```csharp
// Assets/Game/Core/Services/Dialog/DialogService.cs  (modified)
private void EmitCurrentLine() {
    isCurrentLineFullyRevealed = false;
    var line = currentNode.lines[currentLineIndex];
    PlayLineSound(currentNode.speaker, line.soundId);
    SetViewState(DialogViewMode.Line, currentNode.speaker, line.text, null);
}

private void PlayLineSound(string speaker, string soundId) {
    var library = G.Config.DialogSoundLibrary;
    if (library == null || G.Audio == null) {
        return;
    }
    var cue = library.Resolve(speaker, soundId);
    if (cue != null) {
        G.Audio.Play2D(cue);
    }
}

private string GetCurrentLineText() {
    // ...
    return currentNode.lines[currentLineIndex].text;  // was: return currentNode.lines[currentLineIndex];
}
```

### DialogPanel Cleanup

Remove from `DialogPanel.cs`:
- `[SerializeField] private AudioCue defaultTalkSound;`
- `[SerializeField] private SpeakerSound[] speakerSounds;`
- `struct SpeakerSound`
- `PlayTalkSound()` and `FindSpeakerSound()` methods
- `PlayTalkSound(state.Speaker)` call in `ShowLine()`

## File Change Summary

| File | Action |
|------|--------|
| `Assets/Game/Core/Models/Dialog/DialogLine.cs` | **Create** — `[Serializable]` class with `text`, `soundId` |
| `Assets/Game/Audio/Dialog/CharacterSoundProfile.cs` | **Create** — SO with default talk sound + named sound entries |
| `Assets/Game/Audio/Dialog/DialogSoundLibrary.cs` | **Create** — SO registry with fallback resolution |
| `Assets/Game/Core/Models/Dialog/DialogNode.cs` | **Modify** — `string[] lines` → `DialogLine[] lines` |
| `Assets/Game/Core/Services/Dialog/DialogService.cs` | **Modify** — read `.text` from lines; add `PlayLineSound()` |
| `Assets/Game/Configs/MainConfig.cs` | **Modify** — add `DialogSoundLibrary` field |
| `Assets/Game/UI/Dialog/DialogPanel.cs` | **Modify** — remove audio fields, methods, and `PlayTalkSound` call |
| `Assets/Game/Resources/Locale/en/Dialogs/merchant.en.json` | **Modify** — convert lines to `{ "text": "..." }` format |

## Unity Editor Steps (after code changes)

1. Note current AudioCue assignments on `DialogPanel` prefab (`defaultTalkSound`, `speakerSounds`) before recompiling — these need to be re-wired into the new library.
2. Create folder `Assets/Game/Audio/Dialog/`.
3. Create `DialogSoundLibrary.asset` (right-click → Audio/Profiles/Dialog/DialogSoundLibrary).
4. Per character, create a subfolder (e.g. `Merchant/`) and a `CharacterSoundProfile.asset` inside it.
5. Wire the AudioCue references from the old DialogPanel into the new profile assets.
6. Wire character profiles into `DialogSoundLibrary.asset`.
7. Open `MainConfig` asset (`Assets/Game/Resources/Configs/MainConfig`) and assign the `DialogSoundLibrary` field.

## Verification

- Start the merchant dialog → lines display with typewriter as before.
- Lines with a `soundId` play the matching AudioCue from the character profile.
- Lines without `soundId` play the character's default talk sound.
- A speaker with no profile falls back to the global default sound.
- `G.Config.DialogSoundLibrary` being null causes no errors (graceful no-op).
- Choices, conditions, and actions still work correctly.