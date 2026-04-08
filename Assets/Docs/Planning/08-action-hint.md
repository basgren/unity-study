# 08 - Interaction Priority and Action Hint

## Goal

Add a single interaction-selection flow that:

- chooses the best target when several interactions are available at once,
- lets draggable barrels win over lower-priority nearby interactables,
- shows a bottom-center caption with the current `Interact` binding and localized action text,
- stays extendable for future interactables without adding more special cases.

Examples from the requested behavior:

- If the hero is near both a `DialogNPC` and a `DraggableBarrel`, pressing `Interact` should start barrel dragging, not dialog.
- If a barrel overlaps a `Bonfire`, `Helm`, `InfoSign`, or `Chest`, the barrel should still be the chosen target when the player is in position to drag it.
- The hint should display the current locale text:
  - `Grab / Схватить`
  - `Rest / Отдохнуть`
  - `Turn / Повернуть`
  - `Read / Прочитать`
  - `Open / Открыть`

## Current State

The current interaction flow is split across two unrelated systems:

- `PlayerController` keeps a trigger-based list of `InteractableBase` objects and always picks the closest one (distance from `transform.position`).
- `DragAbility` does its own `OverlapCircle` check at `interactPoint` (the `GrabPoint` Transform on the Hero prefab) and also reacts to the same `Interact` action.

Relevant files:

- `Assets/Game/Features/Characters/Hero/PlayerController.cs`
- `Assets/Game/Features/Characters/Hero/Abilities/DragAbility.cs`
- `Assets/Game/Core/Components/Interaction/InteractableBase.cs`
- `Assets/Game/Features/Dynamic/DraggableBarrel.cs`

This creates the conflict described above: two systems consume the same input frame, with no shared arbitration step. They also use *different* distance origins, so even when only one fires, "closest" has no consistent meaning across systems.

Other useful facts from the current project:

- `Helm` and `Chest` both rely on the generic `Switch` component, so action text cannot be inferred from component type alone.
- The HUD scene is already loaded additively via `HudService`, and existing widgets bind through `G.Hero`.
- `HeroService` already exposes `Controller` and `ItemUseService` and fires `OnHeroRegistered` / `OnHeroUnregistered`. The interaction resolver should plug into the same registration flow.
- The Hero prefab already has a `GrabPoint` child Transform (used today as `DragAbility.interactPoint`). It is positioned in front of the hero and flips with facing.
- Localization already uses Unity Localization string tables; project code reads from them via `LocalizedString` and `LocalizationSettings.StringDatabase` (see `InfoSign.cs`).
- Input already has `Keyboard&Mouse` and `Gamepad` control schemes in `InputActions`, owned by `InputService`.

## Constraints

- Do not break existing prefab and scene wiring.
- Keep the barrel drag feel unchanged; the refactor must preserve press-to-grab and the existing release/jump/grounded cancel rules.
- Avoid hardcoding hint text inside generic gameplay components like `Switch`.
- Keep the hint pixel-safe and authorable in the existing HUD scene.

## Chosen Approach: Unified Interaction Candidates and Providers (Option C)

Introduce one resolver on the hero that gathers all currently valid interaction candidates from a set of providers, ranks them, exposes the selected target, and triggers only that target. Each source of interaction (trigger interactables, barrel dragging, future contextual interactions) contributes candidates through a small provider contract.

Why this over the alternatives:

- A "minimal special case" patch (let `DragAbility` preempt `PlayerController`) duplicates interaction logic and forces a parallel hint code path for barrels. It does not generalize.
- Adding `priority` directly to `InteractableBase` and refactoring the player to sort by priority handles trigger interactables, but `DraggableBarrel` is not an `InteractableBase` and depends on a front-of-player query plus grounded checks. It would still need a separate adaptation layer for barrels.

Option C is the only path that gives one place to decide what the player will interact with, lets barrel logic keep its contextual checks without pretending to be a normal trigger, and lets the hint UI bind to the same candidate that input execution uses.

### Trade-offs accepted

- Slightly more upfront refactor than a special-case patch.
- Requires moving `Interact` input ownership out of the current split between `PlayerController` and `DragAbility`.

These costs are paid once and unlock a clean extension point for future interactables.

## Design

### 1. Player Interaction Resolver

Create a hero-side component:

- `Assets/Game/Features/Characters/Hero/Interaction/PlayerInteractionResolver.cs`

Responsibilities:

- own the `Interact` input read for the hero,
- gather candidates from registered providers each frame,
- rank candidates and pick the active one,
- drive hover state on the previous and current candidate,
- execute the active candidate on `Interact` press,
- when an executed candidate returns a modal handle, suspend candidate evaluation until the handle reports inactive,
- expose `CurrentCandidate` for the HUD widget.

The resolver should be registered with `HeroService` alongside `ItemUseService` so HUD widgets can reach it via `G.Hero` without scene-global lookups. Add an `Interaction` property on `HeroService` mirroring `ItemUseService`.

Selection rule:

1. higher `Priority` wins,
2. if equal, lower `SqrDistanceFromGrabPoint` wins,
3. if still equal, keep selection stable using provider order or instance id.

This replaces the current split ownership where both `PlayerController` and `DragAbility` read `Interact`.

#### Distance origin

All providers compute distance against the **same reference point**: the existing `GrabPoint` Transform on the Hero prefab. The resolver exposes it as a property and providers read it when populating candidates. This:

- gives one canonical "where the player is reaching" point,
- naturally favors the object the player is facing rather than standing on,
- requires no new prefab wiring (the Transform already exists).

`DragAbility` keeps its `interactPoint` serialized field name to avoid breaking the prefab, but the resolver and the barrel provider both read the same Transform reference.

### 2. Candidate, Provider, and Handle Contracts

```csharp
public interface IInteractionCandidate {
    int Priority { get; }
    LocalizedString ActionText { get; }   // null/empty → no hint shown
    float SqrDistanceFromGrabPoint { get; }
    bool IsValid { get; }                 // contextual checks (e.g., grounded, switch enabled)
    void OnHoverEnter();
    void OnHoverExit();
    IInteractionHandle Execute();         // null for one-shot, handle for modal
}

public interface IInteractionProvider {
    void CollectCandidates(List<IInteractionCandidate> output);
}

public interface IInteractionHandle {
    bool IsActive { get; }                // resolver checks this each frame
}
```

Notes:

- `ActionText` is a `LocalizedString` (not a raw key string). It binds via the inspector picker against the `UI` table, integrates with the existing localization workflow, and re-resolves automatically on locale change. A null or empty `LocalizedString` means "execute the action but show no hint text" (used by `DialogNPC` until its verb is agreed).
- `SqrDistanceFromGrabPoint` is squared to avoid sqrt during ranking.
- `IsValid` lets a candidate disqualify itself contextually (e.g., barrel without ground, switch with `isDisabled == true`) without removing it from the provider's tracking list.
- `IInteractionHandle` is the modal mechanism — see Section 4.

The resolver allocates the candidate buffer once and reuses it across frames to avoid `Update`-time GC.

### 3. Hover State Owned by the Resolver

Drop `IsHovered` from `InteractableBase` and move hover ownership entirely to the resolver via the candidate contract. Reasoning:

- The barrel is not an `InteractableBase`, so today there are already two highlight paths. Keeping `IsHovered` on `InteractableBase` would force any future non-`InteractableBase` interactable to invent a third path.
- The current `PlayerController.OnTriggerExit2D` eagerly clears `IsHovered`, which would race with the resolver after the refactor.

Concretely:

- Replace `InteractableBase.IsHovered` with a public `SetHighlighted(bool)` method that calls the existing `OnHoveredChange` virtual. Subclasses (`Switch`, `Bonfire`, `InfoSign`) keep their visuals; only the entry point name changes.
- The trigger provider wraps each in-range `InteractableBase` in a small adapter candidate whose `OnHoverEnter/Exit` forwards to `SetHighlighted`, and whose `Execute` calls the existing `Interact()` and returns `null`.
- The barrel provider wraps the in-range `DraggableBarrel` in an adapter whose `OnHoverEnter/Exit` forwards to `BarrelHighlighter.SetHighlighted(BarrelHighlightMode.Hover/None)` and whose `Execute` starts the drag and returns a `BarrelDragHandle` (see Section 4).
- The resolver is the **only** caller of `OnHoverEnter/OnHoverExit`. When the selected candidate changes, it exits the previous and enters the new one in the same frame. When a handle becomes active or inactive, hover is suspended/resumed accordingly.
- Remove the eager `IsHovered = false` line in `PlayerController.OnTriggerExit2D`. The trigger provider just removes the object from its in-range list — the resolver handles hover transitions.

Result: future interactables that are neither `InteractableBase` nor `DraggableBarrel` plug in by exposing a method the adapter forwards to. No new inheritance, no new special cases.

### 4. Barrel as a Modal Candidate (press vs hold)

The barrel needs the input across many frames, not just a single press: drag continues until the player releases `Interact`, jumps, or loses ground (or the barrel does). A pure one-shot `Execute()` cannot express this.

Solution: candidates may return an `IInteractionHandle` from `Execute()`. The resolver:

- skips all candidate gathering, ranking, and hover updates while `currentHandle != null && currentHandle.IsActive`,
- resumes normal evaluation when the handle reports inactive.

`DragAbility` becomes the barrel provider:

- It still owns barrel detection (`OverlapCircle` at `GrabPoint`), grounded checks, stack checks, and the `TryStartDragging` / `StopDragging` logic.
- It contributes a single candidate when a barrel is at the grab point and grounded.
- The candidate's `Execute()` calls `TryStartDragging()` and returns a `BarrelDragHandle` that reports `IsActive == true` until `StopDragging()` runs.
- Inside `Update`, the existing release/jump/grounded checks continue to fire `StopDragging()` exactly as today. They no longer read `Actions.Interact.WasPressedThisFrame()` for *starting* a drag — the resolver does that.

Tap-style interactables (`Bonfire`, `Switch`, `InfoSign`, `DialogNPC`) return `null` from `Execute()`, which the resolver treats as one-shot. The fast path costs nothing extra.

Default priorities:

- `DraggableBarrel` candidate: `100`
- ordinary interactables: `0`

This ensures the barrel always wins overlap conflicts. No need to special-case any other prefab unless a future requirement demands it.

### 5. Trigger Provider for Standard Interactables

Move the trigger tracking out of `PlayerController` into a `TriggerInteractionProvider` component on the hero:

- `OnTriggerEnter2D` / `OnTriggerExit2D` keep an in-range list of `InteractableBase` instances.
- `CollectCandidates` wraps each in-range, valid object in an adapter and writes them to the output list.
- The provider does not call `SetHighlighted` directly — only the resolver does, via the adapter's `OnHoverEnter/Exit`.

Small additions to `InteractableBase`:

- `[SerializeField] private int interactionPriority = 0;` — public read accessor.
- `[SerializeField] private LocalizedString actionText;` — public read accessor.
- `public virtual bool CanInteract() => true;` — overridable contextual check (e.g., `Switch` returns `!isDisabled`).
- Replace the `IsHovered` property with `public void SetHighlighted(bool value) { ... }`. Internally still calls `OnHoveredChange(value)` to preserve subclass visual logic.

`actionText` must be authorable per prefab/instance because `Switch` is shared by `Helm` and `Chest`. Do not hardcode `"Turn"` or `"Open"` inside `Switch.cs`.

### 6. HUD Hint Widget

Recommended widget:

- `Assets/Game/UI/Widgets/InteractionHint/InteractionHintWidget.cs`
- prefab: `Assets/Game/UI/Widgets/InteractionHint/InteractionHint.prefab`

Behavior:

- Subscribes to `G.Hero.OnHeroRegistered` / `OnHeroUnregistered` like `HealthBarWidget` already does.
- After registration, reads `G.Hero.Interaction.CurrentCandidate` and listens for change events.
- Hidden when `CurrentCandidate == null` or its `ActionText` is null/empty.
- Visible otherwise; shows `binding display badge + localized action text`.
- Re-resolves the binding display string on control-scheme change (see Section 7).
- Re-resolves the action text via `LocalizedString` (which fires its own change event on locale switch — no manual locale subscription needed).
- Clears itself when the hero unregisters or controls are disabled.

### 7. Binding Display from the Input System

Use `G.Input.Player.Interact.GetBindingDisplayString(...)` to fetch the key label rather than hardcoding letters.

Add a small helper on `InputService`:

- track the last-used control scheme via `InputUser.onChange` (`InputUserChange.ControlSchemeChanged`),
- expose `CurrentScheme` and an `OnSchemeChanged` event,
- fall back to `Keyboard&Mouse` if no scheme has been detected yet.

The widget asks `Interact.GetBindingDisplayString(group: G.Input.CurrentSchemeBindingGroup)` and refreshes on `OnSchemeChanged`. This keeps the caption correct on a gamepad/keyboard switch without hardcoding key names anywhere.

Future-proof: the same plumbing handles interactive rebinding if it is added later, and locale-affected binding labels.

### 8. Localized Action Text

Add action entries to the existing `UI` string table for both `en` and `ru` instead of a hardcoded enum-to-text map:

- `ui.interact.grab`
- `ui.interact.rest`
- `ui.interact.turn`
- `ui.interact.read`
- `ui.interact.open`

Optional later:

- `ui.interact.talk`

Each interactable's `LocalizedString` field is set in the inspector to point at one of these keys. New verbs are added by editing the table and pointing a new prefab at them.

### 9. HUD Presentation

Place the hint in the additive HUD scene, anchored bottom-center.

Visual structure:

- one compact root panel,
- one small key badge on the left (background sprite + TMP text for the key label),
- one TMP text label on the right bound via `LocalizeStringEvent`,
- no animation more complex than a short fade or snap.

Pixel safety:

- integer-friendly anchored position,
- reuse the TMP font/material setup already used by HUD widgets,
- verify readability on `480x270` reference resolution.

## Rollout Plan

### Phase 1 - Interaction Infrastructure

1. Define `IInteractionCandidate`, `IInteractionProvider`, `IInteractionHandle` interfaces under `Assets/Game/Features/Characters/Hero/Interaction/`.
2. Add `PlayerInteractionResolver` component on the hero. It owns the `Interact` input read, the candidate buffer, the ranking, the hover transitions, and the modal handle state.
3. Register the resolver with `HeroService` (`HeroService.Interaction` property, set during `Register`).
4. Remove `CheckInteraction` and the `availableInteractables` list from `PlayerController`. The class keeps movement, attack, and item-use only.
5. Move trigger tracking into a new `TriggerInteractionProvider` component on the hero.
6. Replace `InteractableBase.IsHovered` with `SetHighlighted(bool)`. Update subclasses if any reference `IsHovered` directly (visual code keeps working through `OnHoveredChange`).
7. Add `interactionPriority`, `actionText` (`LocalizedString`), and `CanInteract()` to `InteractableBase`.

### Phase 2 - Barrel Priority Integration

1. Refactor `DragAbility` to act as an `IInteractionProvider`. Stop reading `Actions.Interact.WasPressedThisFrame()` for drag *start*. Keep all release/jump/grounded checks for drag *stop* exactly as today.
2. The barrel candidate's `Execute()` calls `TryStartDragging()` and returns a `BarrelDragHandle`.
3. The handle reports `IsActive == true` until `StopDragging()` runs (set a flag from `StopDragging`).
4. Set the barrel candidate's priority to `100`.
5. Verify overlap cases:
   - barrel + `DialogNPC`
   - barrel + `Bonfire`
   - barrel + `Helm`
   - barrel + `InfoSign`
   - barrel + `Chest`

### Phase 3 - Action Metadata

1. Author `interactionPriority = 0` and the correct `LocalizedString` on:
   - `Bonfire` prefab → `ui.interact.rest`
   - `Helm` prefab → `ui.interact.turn`
   - `InfoSign` prefab → `ui.interact.read`
   - `Chest` prefab → `ui.interact.open`
2. `DialogNPC` instances: keep `interactionPriority = 0`, leave `actionText` empty for now. They still participate in the resolver — barrel still wins on priority. The hint just stays hidden over an NPC until the verb is agreed.
3. (Optional) If `ui.interact.talk` is added, point `DialogNPC` instances at it.

### Phase 4 - HUD Widget

1. Create the `InteractionHintWidget` script.
2. Create the `InteractionHint.prefab` and place it in `Hud.unity`, anchored bottom-center.
3. Bind the widget to the resolver via `G.Hero.OnHeroRegistered`.
4. Resolve action text via the candidate's `LocalizedString`.
5. Resolve the binding display via `G.Input.Player.Interact.GetBindingDisplayString(...)` plus the `InputService` scheme helper.
6. Hide on null candidate, on empty `ActionText`, or on hero unregister.

### Phase 5 - Content Pass and Verification

1. Add the new localization entries to `UI_en.asset` and `UI_ru.asset`.
2. Set priorities and action keys on prefabs and any scene overrides.
3. Test keyboard and gamepad (caption updates on scheme switch).
4. Test interactions while menus, dialog, or bonfire rest transitions are active (caption hides correctly).
5. Test the modal-handle path: drag a barrel past a Bonfire/InfoSign/NPC and confirm the resolver does not flicker the hint or fire other interactions mid-drag.

## Preconditions / Manual Unity Steps

These must be done in the Unity Editor for the feature to render correctly:

### Localization

Add new entries to the `UI` table for both `en` and `ru`:

- `ui.interact.grab` → Grab / Схватить
- `ui.interact.rest` → Rest / Отдохнуть
- `ui.interact.turn` → Turn / Повернуть
- `ui.interact.read` → Read / Прочитать
- `ui.interact.open` → Open / Открыть
- (optional) `ui.interact.talk` → Talk / Поговорить

Files:

- `Assets/Game/L10n/UI_en.asset`
- `Assets/Game/L10n/UI_ru.asset`

### HUD Scene Placeholder

In `Assets/Game/Scenes/Hud.unity`, create a reusable prefab `Assets/Game/UI/Widgets/InteractionHint/InteractionHint.prefab` and place it on the canvas:

- Root: small panel anchored bottom-center, integer-rounded anchored position, initially disabled.
- Child 1 — Key badge: background sprite + TMP text for the key label. Reuse the TMP font/material already used by other HUD widgets.
- Child 2 — Action label: TMP text with a `LocalizeStringEvent` component (or set programmatically by the widget).
- Verify on the `480x270` reference resolution before merging.

### Prefab Metadata

After Phase 1 lands, set the new fields on each prefab:

- `Assets/Game/Features/Interactive/Bonfire/Bonfire.prefab` → priority `0`, action `ui.interact.rest`
- `Assets/Game/Features/Interactive/Helm/Helm.prefab` → priority `0`, action `ui.interact.turn`
- `Assets/Game/Features/Interactive/InfoSign/InfoSign.prefab` → priority `0`, action `ui.interact.read`
- `Assets/Game/Features/Props/Chest/Chest.prefab` → priority `0`, action `ui.interact.open`
- `Assets/Game/Features/Dynamic/Barrel.prefab` → barrel candidate priority is set in code (`100`), action `ui.interact.grab` (carried by the provider, not on `InteractableBase`).
- `DialogNPC` instances → priority `0`, action empty (or `ui.interact.talk` if shipping the verb in this pass).

### Hero Prefab Wiring

- Add the `PlayerInteractionResolver` component to `Assets/Game/Features/Characters/Hero/Hero.prefab`.
- Add the `TriggerInteractionProvider` component to the same prefab.
- Verify the resolver discovers `DragAbility` (and any future provider) via `GetComponents<IInteractionProvider>()` in `Awake`.
- Confirm `DragAbility.interactPoint` still references `GrabPoint` after the refactor — the resolver should read the same Transform.

## Risks

### Split Input Ownership

If `PlayerController` and `DragAbility` both continue to read `Interact`, the priority system will remain unreliable.

Mitigation: centralize `Interact` consumption in the resolver. The `Interact` action is read in exactly one place after this change.

### Modal handle leak

If `BarrelDragHandle.IsActive` is not cleared on every drag-stop path, the resolver will be stuck and no interactions will fire.

Mitigation: set the inactive flag inside `StopDragging()`, which is the single drop path for jump-cancel, release, and grounded loss. Add an editor-only assertion if `dragJoint` is null but the handle is still active for more than one frame.

### Hover/Highlight Regressions

Multiple objects in range could blink highlights if hover transitions are not strict.

Mitigation: the resolver is the only hover caller. On candidate change in the same frame: previous `OnHoverExit` then current `OnHoverEnter`. While a modal handle is active, hover is suspended.

### Generic `Switch` Text

If hint text were derived from component type, `Helm` and `Chest` would both show the wrong label.

Mitigation: action text is a per-instance `LocalizedString` on `InteractableBase`. `Switch.cs` never references action text.

### Device-Specific Binding Display

Without control-scheme tracking, the hint may show keyboard text while the player uses a gamepad.

Mitigation: `InputService` tracks last-used scheme via `InputUser.onChange` and exposes a change event the widget subscribes to.

### UI Noise During Drag, Menus, or Bonfire Rest

The caption may remain visible when controls are disabled or a menu is open.

Mitigation:

- The resolver suspends candidate evaluation while a modal handle is active (drag) and clears `CurrentCandidate` to null.
- The widget hides on null candidate and on hero unregister.
- `Bonfire.DoInteract()` already calls `SetControlsEnabled(false)`, which disables the action map; the resolver should treat a disabled action map as "no candidate" and clear immediately.

## Assumptions

- `DialogNPC` is part of the priority problem, but its action caption text is not included in the current requested translation list. It still registers as a candidate so barrel-vs-dialog priority works; the hint stays empty until a verb is agreed.
- The current request does not require a separate "release" caption while dragging a barrel. While the modal handle is active, the hint is hidden.
- Existing keyboard/gamepad bindings remain the source of truth for the key label; the caption reflects them rather than duplicating key names in localization.
- `GrabPoint` on the Hero prefab remains the canonical reference Transform for interaction reach. If it is moved or repurposed in the future, the resolver and `DragAbility` move together.

## Recommendation Summary

Implement one shared interaction resolver and let both standard interactables and barrel dragging feed candidates into it through a small interface set. Use `LocalizedString` for action text, `GrabPoint` as the single distance origin, and an `IInteractionHandle` for modal interactions like barrel dragging. The resolver is the only owner of hover state and the only consumer of the `Interact` input.

This gives:

- interaction priority without special-case spaghetti,
- a single source of truth for the bottom-center hint,
- per-prefab action text that works for generic components like `Switch`,
- a clean modal pattern for any future "hold to use" interactable,
- a clean extension point for any future interactable that is not an `InteractableBase`.