# Death Vignette (Hero Death Sequence)

When the hero dies, the game briefly slows down, the color drains out of the world, a round "window" closes around the hero's body, and the screen fades to black. Then the hero respawns at the last checkpoint and the world reappears.

This document explains how the whole thing is wired together.

---

## What the Player Sees

1. Hero takes a fatal hit.
2. `Time.timeScale` drops to `0.7` — the world moves slower.
3. The **level music fades out** and a one-shot **death jingle** plays.
4. Colors smoothly desaturate toward grayscale.
5. A **dark ring** appears around the hero (the "iris" — like old cartoons closing on the protagonist).
6. The ring **closes** over ~2 seconds while following the falling body.
7. Once the opening is tiny, the remaining area **fades to solid black**.
8. At full black, the hero respawns (same scene or scene reload).
9. Camera **snaps** to the respawn point, music restarts, world fades back in.

Total time: about **2.5 seconds** on unscaled time (so the slow-motion on gameplay does not stretch the effect).

---

## The Pieces

All the code lives under `Assets/Game/Features/Effects/DeathScreen/`.

| File | Role |
|------|------|
| `DeathScreenEffect.cs` | Controller. Builds the canvas/image/URP Volume at runtime, runs the timeline coroutine. |
| `DeathScreenIris.shader` | UI shader that paints the circle + dithered edge + fade-to-black. |
| `DeathScreenSettings.cs` | Tuning values (serialized on `MainConfig`). |

Supporting pieces that got updated for this feature:

| File | Role |
|------|------|
| `G.cs` / `GInit.cs` | Register the controller as `G.DeathEffect` so anyone can trigger it. |
| `MainConfig.cs` | New `DeathScreen` settings section. |
| `AudioService.cs` / `IAudioService.cs` | Owns the current level music; can duck/resume it. |
| `LevelEntryPoint.cs` | Tells `G.Audio` which cue is the level music. |
| `PlayerController.cs` | Triggers the effect on death, restores camera + music on respawn. |
| `CameraService.cs` (existing) | Tells Cinemachine to cut instead of pan when the target teleports. |

Nothing needs to be wired in the Scene hierarchy — the controller builds its own UI and URP Volume programmatically.

---

## How It's Built (at Runtime)

When the game starts, `GInit` creates a `DeathScreenEffect` GameObject and calls `Init(mainConfig.DeathScreen)`. In its `Awake`, the controller creates a small tree under itself:

```
DeathScreenEffect                (G.DeathEffect)
├── DeathScreen_Canvas           Canvas + CanvasScaler
│   └── DeathScreen_Iris         RectTransform + RawImage + custom Material
└── DeathScreen_Volume           URP Volume (Global) with ColorAdjustments
```

- The **Canvas** is `ScreenSpaceOverlay` — drawn AFTER every URP render pass. That is what guarantees the iris covers sprites, tiles, 2D lights, and post-processing effects.
- The **RawImage** is stretched to fill the screen and uses a custom material created from `DeathScreenIris.shader`.
- The **Volume** is a URP post-processing volume with a `ColorAdjustments` override. The controller sets its `saturation` to `-100` to drain the color.

Creating all this in code means artists/designers do not have to maintain a prefab.

---

## The Timeline (what `Play` does)

`PlayerController` calls `G.DeathEffect.Play(transform, callback)` when the hero dies. `transform` is the hero's transform — the iris follows it. `callback` runs at the end of the effect (triggers scene reload or respawn).

Inside the coroutine `Run`:

```
t = 0
Time.timeScale = 0.7             // gameplay slows down
colorAdjust.saturation enabled   // desaturation starts from 0

while t < totalRespawnDelay (default 2.5):
    t += unscaledDeltaTime        // advance effect time (unaffected by timeScale)

    UpdateShaderCenter(hero)      // iris center follows hero (viewport coords)

    saturation = lerp(0, -100, t / 0.4)        // fast color drain
    irisRadius = lerp(InitialR, FinalR, eased) // delayed + ease-in shrink
    fadeAll    = lerp(0, 1, near the end)      // final black-out

Time.timeScale = 1
callback()                        // respawn / reload
```

Key design choices:

- **Unscaled time**: the effect runs on `Time.unscaledDeltaTime`, so the gameplay slowdown (`0.7` timescale) does not stretch the visual.
- **Follow every frame**: the hero can die mid-air and keep falling — the iris tracks the transform.
- **Three overlapping phases**: desaturation runs fast at the start, iris shrink dominates the middle, fade-to-black finishes it.

---

## The Shader in Plain English

File: `DeathScreenIris.shader`.

Given the current pixel, it decides how dark it should be:

1. **Snap to a virtual pixel grid.** Each pixel's UV is quantized to a grid matching the game's native resolution (e.g. 480×270). Everything downstream uses those snapped coordinates, so the iris edge lands on the same pixels as the sprite art.

2. **Compute distance from center.** `length(snappedUv - snappedCenter)`, with the X axis multiplied by aspect so the circle stays round on a 16:9 screen.

3. **Work out where we are across the edge band.**
   - `t = 0` → fully inside the iris window.
   - `t = 1` → fully outside in the dark ring.
   - `t` between 0 and 1 → we are inside the transition band and the dither decides.

4. **Dither the transition.** A classic **Bayer 4×4 ordered-dither matrix** is sampled at the pixel's virtual coordinate. Each cell has a threshold in `[0/16, 15/16]`. The pixel is "outside" if `t > threshold`, otherwise "inside". Pixels with a low threshold flip early, pixels with a high threshold flip last — you get the familiar stippled/checkered boundary from 8-bit and 16-bit games.

5. **Pick the alpha.**
   - Inside the window → `FadeAll` (0 for most of the effect, 1 at the very end for the fade-to-black).
   - Outside the window → `Darkness` (1 by default — solid black ring).

6. **Output** a pure black color with the chosen alpha, blended on top of the UI.

### Properties the controller feeds in

| Property | What it controls |
|----------|------------------|
| `_Center` | Hero position in viewport space (0..1), updated every frame. |
| `_Radius` | Radius of the clear circle. Animated from `InitialRadius` down to `FinalRadius`. |
| `_EdgeWidth` | Thickness of the dither band. Bigger value = more stippled pixels, softer feel. |
| `_Aspect` | Screen width / height — keeps the circle circular. |
| `_Darkness` | Opacity of the outside ring (usually 1). |
| `_FadeAll` | Opacity of the inside. Ramps 0 → 1 at the end for the fade-to-black. |
| `_Resolution` | How many virtual pixels span the screen (used for snapping + dither). Recomputed from `PixelPerfectCamera` each frame. |
| `_UseDither` | 1 = Bayer dither, 0 = plain smoothstep fallback. |

### `_MainTex` exists but is not used

Unity's UGUI stack writes `material.mainTexture` to every UI material each frame. If the shader does not declare `_MainTex`, Unity logs a runtime warning. We declare it as `[HideInInspector]` and ignore it.

---

## Pixel-Perfect Alignment (the subtle part)

This project uses `PixelPerfectCamera` with a reference resolution of **480×270**. Sprite art is rendered at that size and upscaled to the window (e.g. ×4 at 1920×1080).

Because the iris canvas is `ScreenSpaceOverlay`, the shader actually runs at **full screen resolution** (e.g. 1920×1080). If we snapped to the reference resolution directly, the dither blocks might be 3.33 screen pixels wide on some resolutions — visibly out of step with the sprite pixels.

The fix lives in `DeathScreenEffect.ResolveVirtualResolution()`:

```
scale = round(Screen.height / refResolutionY)   // 1080 / 270 = 4
virtualW = Screen.width  / scale                // 1920 / 4 = 480
virtualH = Screen.height / scale                // 1080 / 4 = 270
```

We compute the exact integer scale `PixelPerfectCamera` is using (`round(Screen.height / 270)`), then divide the full screen by that scale. The resulting "virtual resolution" makes each virtual pixel exactly the same number of screen pixels as a sprite pixel. Dither lines up 1:1.

(We avoid `PixelPerfectCamera.pixelRatio` — in the Editor's Game view it sometimes reports `1`, which collapses the dither to screen-pixels.)

---

## Following the Hero

`PlayerController` does not teleport the hero on death — it just plays the death animation and leaves the body to fall. This is why the iris needs to follow.

Every frame inside `Run`, the controller calls:

```csharp
var vp = Camera.main.WorldToViewportPoint(hero.position);  // (0..1, 0..1)
irisMat.SetVector("_Center", new Vector4(vp.x, vp.y, 0, 0));
```

So if the hero is bounced off a cannon shot, thrown back, or falls off a ledge, the iris keeps tracking the sprite until the fade finishes.

---

## Where the Settings Live

`MainConfig.asset` (`Resources/MainConfig`) now has an **Effects → Death Screen** section — that is the tuning surface. It is a `DeathScreenSettings` instance, inlined on `MainConfig` (no separate asset).

| Field | Typical range | What it does |
|-------|--------------|--------------|
| `SlowTimeScale` | 0.5–1.0 | Gameplay time scale during the sequence. |
| `DesaturateDuration` | 0.2–1.0 s | How fast the color drains. |
| `InitialRadius` / `FinalRadius` | 0.55 / 0.06 | Iris open/close size (aspect-corrected UV units). |
| `IrisStartDelay` | 0.0–0.3 s | Pause before the iris begins shrinking. |
| `IrisShrinkDuration` | 1.5–3.0 s | How long the shrink lasts. |
| `VignetteDarkness` | 0–1 | Ring opacity (1 = solid black). |
| `EdgeWidth` | 0.03–0.1 | Thickness of the dithered edge band. Bigger = more stipple. |
| `UseDither` | bool | Ordered-dither edge (true) vs smoothstep edge (false). |
| `FadeToBlackDuration` | 0.3–0.6 s | Duration of the final inside-fade to solid black. |
| `TotalRespawnDelay` | 2.0–3.0 s | Full length of the sequence. Callback fires at this mark. |

Because `DeathScreenEffect` is created dynamically by `GInit`, it cannot use `[SerializeField]` for config (see AGENTS.md). `GInit` calls `DeathEffect.Init(mainConfig.DeathScreen)` to hand the values over.

---

## How `PlayerController` Uses the Effect

There are two "real death" entry points in the hero script:

1. **`ShowHitAndRestartScene`** — no checkpoint set. After the effect, the active scene is reloaded.
2. **`ShowHitAndRespawnAtCheckpoint`** — a bonfire was lit. After the effect, the hero either teleports (same scene) or a different scene is loaded.

Both do the same four things before starting the effect:

```csharp
Actions.Disable();                  // hero no longer responds to input
isDead = isDiedThisFrame = true;    // triggers death animation
damageable.IgnoreDamage = true;     // no more hits can land
PlayDeathJingle();                  // duck music, play one-shot jingle
G.DeathEffect.Play(transform, callback);
```

The callback runs when the screen is fully black:

```csharp
// Scene reload:
() => G.SceneTravel.ReloadActiveScene()

// Same-scene checkpoint:
() => {
    RespawnAtPosition(bonfirePos);         // teleport, restore HP, enable input
    G.DeathEffect.ResetVisuals();          // clear the black overlay
}
```

`DeathScreenEffect` also subscribes to `G.SceneTravel.AfterTransition`, so after any scene load it automatically clears the overlay and resets saturation.

A third death path — `ShowHitAndRespawnAtSafePoint` (falling in water) — is a minor respawn and intentionally does NOT run the death effect.

---

## Camera Snap on Respawn

Without extra work, Cinemachine would smoothly **pan** from the death location to the checkpoint — which feels wrong after a full fade-to-black. Fix:

`CameraService.NotifyTargetTeleported(target, delta)` flags the Cinemachine virtual camera's `PreviousStateIsValid = false` and calls `OnTargetObjectWarped(target, delta)`. Together, those tell the camera "this target jumped — cut, do not interpolate".

`PlayerController` has a `TeleportAndNotifyCamera(pos)` helper used by every respawn path (safe-point, same-scene checkpoint, cross-scene checkpoint). The iris finishes black, the callback runs, and the first frame after the overlay clears is already framed on the hero.

---

## Music Ducking

Level music is owned by `AudioService`, not by the hero.

- `LevelEntryPoint.Start()` → `G.Audio.SetLevelMusic(levelMusic)` — assigns + starts the track.
- `LevelEntryPoint.OnDestroy()` → `G.Audio.ClearLevelMusic()` — stops + forgets.
- On death: `G.Audio.StopLevelMusic()` ducks the track with a short fade. The cue assignment is kept.
- On same-scene respawn: `G.Audio.StartLevelMusic()` restarts the assigned cue from the beginning.
- On cross-scene respawn / scene reload: the new `LevelEntryPoint.Start` takes over.

The one-shot death jingle is a regular `AudioCue` assigned on the hero prefab (`PlayerController.deathJingleCue`) and played via `G.Audio.Play2D(...)`.

---

## Quick Mental Model

```
hero dies
    │
    ├─ PlayDeathJingle()                → stop music, play jingle
    └─ G.DeathEffect.Play(hero, done)   → run 2.5 s timeline
            │
            ├─ desaturate, iris closes, iris fades to black
            │   (follows hero every frame)
            └─ done() fires at full black
                    ├─ reload scene           (no checkpoint)
                    └─ teleport + reset UI    (same-scene checkpoint)
                           └─ camera snaps to hero, music resumes
```

That is the whole system. The shader does the visuals, the controller drives the timeline, and `PlayerController` hooks the two death paths into it.

---

## Manual Editor Steps (checklist)

Once, per project:
1. On the **Main Camera** in `GlobalRoot.prefab`, set **Rendering → Post Processing = ON** (URP Volume needs it for the desaturation to be visible; the controller also flips it on at runtime as a safety net).
2. For builds: **Project Settings → Graphics → Always Included Shaders** — add `Game/UI/DeathScreenIris`. Editor Play mode works without this.

Per-scene:
- Keep `LevelEntryPoint` in each level scene and assign its `levelMusic` cue — that is how music ownership gets transferred to `G.Audio`.

Per-hero:
- Assign `deathJingleCue` on the `PlayerController` component of the Hero prefab.

Tuning:
- Open `Resources/MainConfig.asset` → **Effects → Death Screen**. Every knob described above lives there.
