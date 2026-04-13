# 09 - Grappling Hook Perk

## Goal

Implement a grappling hook perk that lets the hero attach to anchor points in the level and swing from them.

Behaviour:

1. The hook operates only within a configurable radius around the hero.
2. It fires only when at least one **anchor** (ring, hook point, etc.) exists within that radius.
3. On activation the hook projectile travels toward the nearest anchor and sticks to it. A rope connects the hook tip to the hero.
4. While the **use button is held**, the hook stays attached. The hero is constrained by a rope of fixed length and can swing like a pendulum. Horizontal input applies force to influence the swing.
5. When the **use button is released**, the hook detaches from the anchor and retracts back to the hero.
6. The rope is rendered as a chain of small sprites and follows simplified physics (visual sag/wave).

## Current State

### Perk system

Perks implement `IItemUseStrategy` (`Assets/Game/Features/Characters/Hero/ItemUse/IItemUseStrategy.cs`):

```csharp
public interface IItemUseStrategy {
    ItemId ItemId { get; }
    bool CanUse();
    void Use();
    void Update(float deltaTime);
}
```

- `Use()` is called once when the perk button is pressed (gated by `WasPerformedThisFrame()` + cooldown).
- `Update(float deltaTime)` is called every frame unconditionally.
- Strategies are registered in `PlayerController.InitItemUseService()` on the `perkUseService`.
- Existing perks: `ProtectionMaskStrategy` (timed invulnerability), `ParrotDeployStrategy` (companion).
- `ProtectionMaskStrategy` activates in `Use()`, manages its lifecycle in `Update()`, and cleans up when done --- exactly the pattern the hook should follow.

### Input

- Action: `UsePerk`, type `Button` (keyboard `E`, gamepad RT).
- Currently consumed via `Actions.UsePerk.WasPerformedThisFrame()` in `PlayerController.CheckPerkUse()`.
- The `canceled` callback is wired in generated `InputActions.cs`, so `WasReleasedThisFrame()` and `IsPressed()` are available without any input asset changes.
- The strategy's `Update()` runs every frame, so it can poll hold/release state.

### Movement

- `BaseCharacterController.SetDirection()` sets `Rigidbody2D.velocity.x` directly each frame.
- Rigidbody2D config: mass = 10, gravity scale = 1, project gravity = -40 y/s^2, rotation frozen.
- Jump sets `velocity.y` directly with sustain/coyote window.
- All physics in `Update()` (not `FixedUpdate`) per existing project convention, with a comment acknowledging the trade-off.

### Physics joints

- Only `FixedJoint2D` used so far --- in `DragAbility` for barrel dragging.
- Added/removed at runtime via `AddComponent<FixedJoint2D>()` / `Destroy(joint)`.
- No `DistanceJoint2D`, `HingeJoint2D`, or rope systems exist.

### Rendering

- Pixel art at 32 PPU. Reference resolution 480x270.
- All visuals are sprite-based. No `LineRenderer` anywhere in the project.
- `SimpleSpriteAnimator` exists for frame-by-frame sprite animation.

### FSM

- `SimpleStateMachine<TState>` in `Core.FSM` (`Assets/Game/Core/FSM/SimpleStateMachine.cs`).
- Supports: immediate transitions (`Go`), delayed transitions (`GoLater`), exit-time transitions (`PermitAfter`), conditional transitions (`PermitIf`), any-state transitions (`PermitFromAny`).
- `OnTransition` event for enter/exit logic.

---

## Rope Physics --- Approach Options

### Approach A --- Verlet Integration (Custom Rope Simulation)

Simulate N rope points using Verlet integration each `FixedUpdate`. Pin one end to the anchor, one to the hero. Apply gravity to intermediate points. Enforce distance constraints between consecutive points over several solver iterations.

**Pros**
- Full visual control over rope shape (sag, wave, elasticity).
- Pixel-art-friendly: point positions are fully controlled.
- Decoupled from Unity joint system, so it cannot destabilize the player's Rigidbody.
- The player constraint would be enforced separately; rope is visual-only.

**Cons**
- Requires writing and tuning a Verlet solver loop.
- Does not constrain the player by itself --- needs a second system for the actual swing physics.
- Rope has no collision with level geometry.

### Approach B --- Unity Joint Chain

Create N small GameObjects with `Rigidbody2D` + `HingeJoint2D` connecting them. First link anchored to the hook point, last connected to the player.

**Pros**
- Real physics interactions (rope can collide with platforms).
- Less custom simulation code.

**Cons**
- Extremely unstable with the project's high mass (10) and strong gravity (-40). Chain links jitter and stretch.
- Very hard to get pixel-perfect appearance since Unity drives link positions.
- Performance overhead from N Rigidbody2D instances.
- Existing movement system sets `velocity.x` directly every frame, which will fight joint constraints violently.
- Difficult to tune and debug.

**Verdict:** Not recommended. Too unstable for the project's physics configuration and velocity-based movement model.

### Approach C --- Hybrid: Verlet Visual + DistanceJoint2D Constraint (Recommended)

Use a single `DistanceJoint2D` from the player's Rigidbody2D to the anchor world point to enforce the maximum-length constraint. Use Verlet integration purely for the **visual** rope between the two endpoints.

**Pros**
- The player constraint is a single, stable Unity joint (no chain).
- The visual rope is fully controlled and pixel-art-friendly.
- Best of both worlds: correct physical swing behaviour from the joint, pretty rope visual from Verlet.
- Less custom physics code because Verlet only drives visuals, not the player.
- Matches the existing pattern of adding/removing joints at runtime (`DragAbility`).

**Cons**
- The visual rope does not wrap around geometry (acceptable for a simple grapple mechanic).
- The velocity-based movement in `SetDirection()` will fight the joint --- solvable by suppressing horizontal override during swing (see Movement Integration).

**Verdict:** Recommended. Simple, stable, pixel-art-compatible.

---

## Rope Rendering --- Approach Options

### Approach A --- Sprite Chain (Recommended)

Instantiate small rope-segment sprites (e.g. 4x4 or 4x8 pixel link sprites) at each Verlet point. Each sprite is rotated to face the next point.

**Pros**
- Pixel-perfect by definition.
- Consistent with the project's all-sprite rendering approach.
- Simple: each segment is a positioned + rotated `Transform` with a `SpriteRenderer`.

**Cons**
- Many SpriteRenderers (8--12). Negligible overhead for a single rope.

### Approach B --- LineRenderer with Pixel-Art Material

Use `LineRenderer` with a tiling pixel-art rope texture.

**Pros**
- Single draw call. Fewer GameObjects.

**Cons**
- No `LineRenderer` exists anywhere in the project --- introduces a new rendering pattern.
- Width, anti-aliasing, and texture filtering require careful material/shader setup to not break pixel art.
- Harder to achieve individual-link look.

**Verdict:** Not recommended. Introduces inconsistency and risks pixel-art quality.

### Approach C --- Procedural Mesh

Generate a quad-strip mesh along the Verlet points.

**Pros**
- Full UV control. Single draw call.

**Cons**
- Significant complexity for a simple rope. No existing procedural mesh patterns in the project.

**Verdict:** Overkill.

---

## Recommended Design

**Rope physics:** Approach C (hybrid --- `DistanceJoint2D` + Verlet visual).
**Rope rendering:** Approach A (sprite chain).

---

## Component Architecture

The grappling hook is implemented as a **separate MonoBehaviour** (`GrapplingHookAbility`) on the Hero prefab, following the same pattern as `DragAbility`. This avoids bloating `PlayerController` with hook-specific logic and serialized fields. The `IItemUseStrategy` implementation is a thin adapter that delegates to the ability.

```
Assets/Game/Features/Characters/Hero/GrapplingHook/
    GrapplingHookAbility.cs           -- MonoBehaviour on Hero, owns all hook logic and config
    GrapplingHookStrategy.cs          -- thin IItemUseStrategy adapter, delegates to ability
    GrapplingHookProjectile.cs        -- MonoBehaviour on the hook projectile prefab
    GrapplingHookRope.cs              -- MonoBehaviour: Verlet sim + sprite chain rendering
    GrapplingHookAnchor.cs            -- MonoBehaviour marker on anchor objects in scenes
```

Prefabs (under the same folder or a `Prefabs/` subfolder):

```
    GrapplingHook.prefab              -- Hook tip sprite + GrapplingHookProjectile + Rigidbody2D (kinematic)
    RopeSegment.prefab                -- Single rope link sprite (4x4 or 4x8 pixels)
    GrapplingHookAnchor.prefab        -- Anchor ring sprite + GrapplingHookAnchor + CircleCollider2D (trigger)
```

**Responsibility split:**

| Concern | Owner |
|---|---|
| Serialized config (prefabs, radius, layer, force) | `GrapplingHookAbility` |
| FSM, projectile, rope, joint lifecycle | `GrapplingHookAbility` |
| Input polling (hold/release) | `GrapplingHookAbility` |
| Swing force application | `GrapplingHookAbility` |
| `CanUse()` / `Use()` / `Update()` perk interface | `GrapplingHookStrategy` (delegates to ability) |
| `SetHookSwingMode()` + movement guard | `PlayerController` (minimal, same pattern as `SetDragMode`) |

---

## State Machine

```
enum HookState {
    Idle,
    Shooting,
    Attached,
    Retracting
}
```

```
                  Use()
     Idle ──────────────► Shooting
      ▲                     │
      │                     │ hook arrives & button held
      │                     ▼
      │                  Attached
      │                     │
      │  button released    │ button released
      │  (from any active)  │
      │                     ▼
      └──────────────── Retracting
                   (hook returns to hero)
```

Transitions:
- `Idle → Shooting` --- triggered by `Use()`.
- `Shooting → Attached` --- hook projectile reaches anchor while button is held.
- `Shooting → Retracting` --- button released during flight, or anchor destroyed.
- `Attached → Retracting` --- button released.
- `Retracting → Idle` --- hook returns to hero position.
- Any → `Idle` --- forced abort (hero takes damage, anchor destroyed).

Using `SimpleStateMachine<HookState>`:

```csharp
public class GrapplingHookFsm : SimpleStateMachine<HookState> {
    public GrapplingHookFsm() : base(HookState.Idle) {
        Permit(HookState.Idle, HookState.Shooting);
        Permit(HookState.Shooting, HookState.Attached, HookState.Retracting);
        Permit(HookState.Attached, HookState.Retracting);
        Permit(HookState.Retracting, HookState.Idle);
        PermitFromAny(HookState.Idle); // forced abort
    }
}
```

---

## Component Details

### GrapplingHookAnchor

Simple marker component placed on anchor GameObjects in scenes.

```csharp
// Assets/Game/Features/Characters/Hero/GrapplingHook/GrapplingHookAnchor.cs
[RequireComponent(typeof(CircleCollider2D))]
public class GrapplingHookAnchor : MonoBehaviour { }
```

- **Layer:** new physics layer `HookAnchors` (index 19, currently unused).
- **Collider:** `CircleCollider2D`, `isTrigger = true` (does not block movement).
- Detection: `Physics2D.OverlapCircleNonAlloc(heroPos, hookRadius, results, anchorLayer)` to find anchors within radius. Pick the closest.

### GrapplingHookProjectile

MonoBehaviour on the hook prefab. Does **not** extend `ProjectileBase` because the hook travels to a specific target (not indefinitely in a direction) and has retract behaviour.

```csharp
// Assets/Game/Features/Characters/Hero/GrapplingHook/GrapplingHookProjectile.cs
public class GrapplingHookProjectile : MonoBehaviour {
    [SerializeField] private float travelSpeed = 25f;

    public bool HasArrived { get; private set; }
    public bool HasReturned { get; private set; }

    public void LaunchToward(Vector2 target) { ... }
    public void ReturnTo(Transform hero) { ... }
}
```

Movement: kinematic `Rigidbody2D`, `MovePosition` in `FixedUpdate`, straight line toward the anchor. On arrival, stops and sets `HasArrived = true`. On retract, moves toward the hero each frame until within one pixel, then sets `HasReturned = true`.

### GrapplingHookRope

Verlet simulation + sprite chain rendering.

```csharp
// Assets/Game/Features/Characters/Hero/GrapplingHook/GrapplingHookRope.cs
public class GrapplingHookRope : MonoBehaviour {
    [SerializeField] private GameObject segmentPrefab;
    [SerializeField] private int segmentCount = 10;
    [SerializeField] private int constraintIterations = 5;
    [SerializeField] private float gravityScale = 20f;
    [SerializeField] private float damping = 0.98f;
}
```

**Verlet update (FixedUpdate):**
1. For each non-pinned point: `newPos = current + (current - previous) * damping + gravity * dt^2`.
2. Pin point 0 to anchor, point N to hero.
3. Run `constraintIterations` passes of distance correction between consecutive points.

**Rendering (LateUpdate):**
1. Position each segment sprite at its Verlet point.
2. Rotate each sprite to face the next point.

When the hook is in `Shooting` or `Retracting` state, rope endpoints are the hook tip and the hero. Segment rest length is the current endpoint distance divided by segment count (rope adapts dynamically).

### GrapplingHookAbility

The main component. MonoBehaviour on the Hero prefab --- owns all hook config, logic, and lifecycle. Follows the `DragAbility` pattern.

```csharp
// Assets/Game/Features/Characters/Hero/GrapplingHook/GrapplingHookAbility.cs
public class GrapplingHookAbility : MonoBehaviour {
    [Header("Detection")]
    [SerializeField] private float hookRadius = 8f;
    [SerializeField] private LayerMask anchorLayer;

    [Header("Prefabs")]
    [SerializeField] private GameObject hookPrefab;
    [SerializeField] private GameObject ropeSegmentPrefab;

    [Header("Swing")]
    [SerializeField] private float swingInfluenceForce = 150f;

    // Internal state
    private PlayerController player;
    private GrapplingHookFsm fsm;
    private GrapplingHookProjectile activeHook;
    private GrapplingHookRope activeRope;
    private GrapplingHookAnchor targetAnchor;
    private DistanceJoint2D swingJoint;
    private bool isPerkButtonHeld;

    public HookState State => fsm.State;

    // --- Public API called by GrapplingHookStrategy ---

    public bool CanActivate() { ... }
    public void Activate() { ... }
    public void Tick(float deltaTime) { ... }
}
```

**Awake:** caches `PlayerController` reference via `GetComponent<PlayerController>()`.

**CanActivate():**
```csharp
public bool CanActivate() {
    return fsm.State == HookState.Idle
        && FindNearestAnchor() != null;
}
```

**Activate():**
1. Find nearest anchor via `Physics2D.OverlapCircleNonAlloc`.
2. Instantiate hook prefab at hero position.
3. Call `hookProjectile.LaunchToward(anchor.transform.position)`.
4. Create rope visual (`GrapplingHookRope`).
5. `fsm.Go(HookState.Shooting)`.
6. Set `isPerkButtonHeld = true`.

**Tick(float deltaTime)** --- called from strategy's `Update()`:
```csharp
public void Tick(float deltaTime) {
    if (fsm.State == HookState.Idle) {
        return;
    }

    // Track input release
    if (player.Actions.UsePerk.WasReleasedThisFrame()) {
        isPerkButtonHeld = false;
    }

    // Forced abort: hero hit or anchor destroyed
    if (ShouldForceAbort()) {
        CleanupAndGoIdle();
        return;
    }

    switch (fsm.State) {
        case HookState.Shooting:
            UpdateShooting();
            break;
        case HookState.Attached:
            UpdateAttached(deltaTime);
            break;
        case HookState.Retracting:
            UpdateRetracting();
            break;
    }

    // Rope visual always tracks endpoints
    activeRope?.UpdateEndpoints(GetHookPosition(), GetHeroPosition());
}
```

### GrapplingHookStrategy

Thin adapter that bridges `IItemUseStrategy` to `GrapplingHookAbility`. Contains no logic of its own --- just delegates.

```csharp
// Assets/Game/Features/Characters/Hero/GrapplingHook/GrapplingHookStrategy.cs
public class GrapplingHookStrategy : IItemUseStrategy {
    public ItemId ItemId => ItemIds.GrapplingHook;

    private readonly PlayerController controller;
    private readonly GrapplingHookAbility ability;

    public GrapplingHookStrategy(PlayerController controller, GrapplingHookAbility ability) {
        this.controller = controller;
        this.ability = ability;
    }

    public bool CanUse() {
        return ability != null
            && ability.CanActivate()
            && controller.State.InventoryModel.GetCount(ItemId) > 0;
    }

    public void Use() {
        ability.Activate();
    }

    public void Update(float deltaTime) {
        ability.Tick(deltaTime);
    }
}
```

---

## DistanceJoint2D Configuration

```csharp
// Inside GrapplingHookAbility:
private void AttachToAnchor() {
    var heroPos = player.transform.position;
    var anchorPos = targetAnchor.transform.position;

    swingJoint = player.gameObject.AddComponent<DistanceJoint2D>();
    swingJoint.autoConfigureDistance = false;
    swingJoint.distance = Vector2.Distance(heroPos, anchorPos);
    swingJoint.maxDistanceOnly = true;     // rope, not rod: allows slack
    swingJoint.enableCollision = false;
    swingJoint.connectedBody = null;       // anchor to world point (no RB on anchor)
    swingJoint.connectedAnchor = anchorPos;

    player.SetHookSwingMode(true);
    fsm.Go(HookState.Attached);
}
```

`maxDistanceOnly = true` is critical --- it turns the joint into a rope rather than a rigid rod. The hero can be closer than rope length (slack), but cannot exceed it. This produces natural pendulum behaviour when the hero falls off a ledge while attached.

`connectedBody = null` anchors to a world point. Simpler and more stable than adding a kinematic Rigidbody to the anchor.

---

## Input Handling

No changes to the input action asset are needed. The `Button` type already supports `started`, `performed`, and `canceled` phases.

The ability reads input in `Tick()`:
- `player.Actions.UsePerk.WasReleasedThisFrame()` --- detects release.
- The `isPerkButtonHeld` flag is set `true` in `Activate()` and cleared when release is detected.

Cooldown note: `ItemUseService.TryUseSelectedItem()` starts a cooldown after `Use()`. Since `CanUse()` returns `false` while the FSM is not `Idle`, the cooldown acts as an additional grace period after retraction.

---

## Movement Integration

During the `Attached` state, normal horizontal movement must be suppressed. `SetDirection()` sets `velocity.x` directly, which would fight the `DistanceJoint2D` constraint.

**Changes to PlayerController:**

```csharp
// New flag and method (follows the existing isDragging / SetDragMode pattern)
private bool isHookSwinging;

public void SetHookSwingMode(bool swinging) {
    isHookSwinging = swinging;
}

// Guard in CheckHorizontalMovement():
private void CheckHorizontalMovement() {
    if (isHookSwinging) {
        return; // movement handled by hook strategy via forces
    }
    // ... existing code ...
}
```

**Swing input (in ability's UpdateAttached):**

```csharp
// Inside GrapplingHookAbility:
var dir = player.Actions.Move.ReadValue<Vector2>();
if (Mathf.Abs(dir.x) > 0.1f) {
    player.MyRigidbody.AddForce(new Vector2(dir.x * swingInfluenceForce, 0f));
}
```

This applies a horizontal force to influence the pendulum swing, rather than directly setting velocity.

Jump while attached: suppress normal jump or use it to detach with a momentum-preserving boost (design choice to decide during implementation).

---

## Edge Cases

### Hero takes damage while hooked

Check `player.Damageable.IsHitThisFrame` in the ability's `Tick()` during `Shooting` and `Attached` states. If hit, force-detach immediately (`CleanupAndGoIdle()`).

### Anchor destroyed

Check `targetAnchor == null` (Unity's null for destroyed objects) each frame during `Shooting` and `Attached`. If null, force-detach.

### Hero lands on ground while attached

`DistanceJoint2D` with `maxDistanceOnly = true` naturally allows this. When the hero is on the ground and the rope is slack, the constraint is inactive. Normal movement stays suppressed while attached; the hero can only influence via swing force. Detach happens only on button release.

### Controls disabled (death, cutscene)

When `Actions.Disable()` is called, `IsPressed()` returns false and `WasReleasedThisFrame()` may fire. The `isPerkButtonHeld` flag will clear, triggering natural detach. The damage check above also handles the death case.

---

## Registration and Wiring

`GrapplingHookAbility` is a MonoBehaviour added to the Hero prefab. It holds all serialized config in the Inspector --- no hook-related fields on PlayerController.

In `PlayerController.InitItemUseService()`, register the thin strategy:

```csharp
var hookAbility = GetComponent<GrapplingHookAbility>();
if (hookAbility != null) {
    perkUseService.Register(new GrapplingHookStrategy(this, hookAbility));
}
```

The `if` guard means the hook perk is opt-in: if the ability component is not on the Hero prefab, nothing registers and nothing breaks.

**PlayerController changes (minimal):**
- One new flag + method: `SetHookSwingMode(bool)` + guard in `CheckHorizontalMovement()` (same pattern as `SetDragMode`).
- One line in `InitItemUseService()` to register the strategy.
- No new serialized fields on PlayerController.

**Other setup:**
- Add `GrapplingHook` item to `InventoryItemsDef.asset` with `ItemType.Perk`, cooldown ~2s.
- Auto-generator creates `ItemIds.GrapplingHook` in `ItemIds.cs`.
- Add physics layer `HookAnchors` at index 19 (first unused) in TagManager.
- Create pixel art: hook tip (~8x8 to 16x16), rope link segment (~4x4 or 4x8).

---

## Implementation Stages

### Stage 1 --- Foundation

1. Create `GrapplingHookAnchor.cs` --- marker component.
2. Add `HookAnchors` physics layer to project settings.
3. Create `GrapplingHookAnchor.prefab` --- sprite + trigger collider + component.
4. Add `GrapplingHook` item to `InventoryItemsDef.asset`.

### Stage 2 --- State Machine

5. Create `GrapplingHookFsm` extending `SimpleStateMachine<HookState>`.
6. Define all transitions.

### Stage 3 --- Hook Projectile

7. Create `GrapplingHookProjectile.cs` --- kinematic movement + retract.
8. Create `GrapplingHook.prefab` --- sprite + kinematic Rigidbody2D + component.

### Stage 4 --- Ability + Strategy (no rope, no swing)

9. Create `GrapplingHookAbility.cs` MonoBehaviour with serialized config and lifecycle logic.
10. Create `GrapplingHookStrategy.cs` --- thin `IItemUseStrategy` adapter delegating to ability.
11. Add ability component to Hero prefab. Register strategy in `PlayerController.InitItemUseService()`.
12. **Test:** hook launches to anchor, arrives, retracts on release. No swing yet.

### Stage 5 --- Swing Physics

13. Add `DistanceJoint2D` creation/destruction in ability's `AttachToAnchor()` / `StartRetract()`.
14. Add `SetHookSwingMode()` to `PlayerController` (minimal: flag + guard in `CheckHorizontalMovement()`).
15. Add swing force application in ability's `UpdateAttached()`.
16. **Test:** hero swings on pendulum, horizontal input influences swing.

### Stage 6 --- Rope Visual

18. Create rope link sprite (pixel art, 4x4 or 4x8).
19. Create `RopeSegment.prefab` with SpriteRenderer.
20. Create `GrapplingHookRope.cs` --- Verlet sim + sprite positioning.
21. Wire rope into strategy lifecycle.
22. **Test:** rope renders correctly, follows physics with visual sag.

### Stage 7 --- Polish

23. Damage detection and force-detach.
24. Anchor-destroyed detection.
25. Editor gizmo for hook radius (on `GrapplingHookAnchor` or player).
26. Tune parameters: hook speed, swing force, rope gravity, segment count, damping.
27. Sound effects: launch, attach, detach, retract.
28. Visual: anchor highlight when in range (optional).

---

## Key Tuning Parameters

| Parameter | Location | Default | Notes |
|---|---|---|---|
| `hookRadius` | GrapplingHookAbility | 8 | Detection radius in world units (~256 pixels) |
| `travelSpeed` | GrapplingHookProjectile | 25 | Hook flight speed |
| `swingInfluenceForce` | GrapplingHookAbility | 150 | With mass 10, gives ~15 m/s^2 horizontal accel |
| `segmentCount` | GrapplingHookRope | 10 | Number of Verlet points in the rope |
| `constraintIterations` | GrapplingHookRope | 5 | Verlet solver passes (higher = stiffer rope) |
| `ropeGravity` | GrapplingHookRope | 20 | Visual rope gravity (lighter than player's -40) |
| `damping` | GrapplingHookRope | 0.98 | Rope oscillation damping |
| `cooldown` | InventoryItemsDef asset | 2 | Seconds before re-use after retract |

---

## Risks and Mitigations

**`DistanceJoint2D` fights with `SetDirection()` velocity override.**
Mitigation: `isHookSwinging` flag suppresses `SetDirection()` entirely during swing. Follows the existing `isDragging` / `SetDragMode()` pattern.

**Player launched at extreme velocity when rope tightens.**
Mitigation: `maxDistanceOnly = true` prevents snapping. The joint smoothly constrains. If still too aggressive, add a velocity clamp during swing.

**Verlet rope visual desyncs from actual positions.**
Mitigation: Endpoints are pinned to actual transform positions every frame. Only intermediate points are simulated.

**Wrong anchor picked when multiple overlap.**
Mitigation: `OverlapCircleNonAlloc` + iterate to find closest by `sqrMagnitude`.

**Cooldown starts on Use() but hook is still active.**
Mitigation: `CanUse()` returns false while FSM is not Idle, so the cooldown only matters after the full cycle completes.

---

## Files Summary

### New files

| File | Purpose |
|---|---|
| `Assets/Game/Features/Characters/Hero/GrapplingHook/GrapplingHookAbility.cs` | MonoBehaviour on Hero, owns all hook logic and config |
| `Assets/Game/Features/Characters/Hero/GrapplingHook/GrapplingHookStrategy.cs` | Thin IItemUseStrategy adapter |
| `Assets/Game/Features/Characters/Hero/GrapplingHook/GrapplingHookFsm.cs` | State machine |
| `Assets/Game/Features/Characters/Hero/GrapplingHook/GrapplingHookProjectile.cs` | Hook projectile MonoBehaviour |
| `Assets/Game/Features/Characters/Hero/GrapplingHook/GrapplingHookRope.cs` | Verlet rope sim + sprite rendering |
| `Assets/Game/Features/Characters/Hero/GrapplingHook/GrapplingHookAnchor.cs` | Anchor marker component |

### Modified files

| File | Change |
|---|---|
| `Assets/Game/Features/Characters/Hero/PlayerController.cs` | Add `SetHookSwingMode()`, guard in `CheckHorizontalMovement()`, register strategy (no new serialized fields) |
| `Assets/Game/Resources/DefsFacade.asset` (via Inspector) | Add GrapplingHook item definition |
| `ProjectSettings/TagManager.asset` (via Inspector) | Add HookAnchors layer |

### New assets (created in Unity Editor)

| Asset | Description |
|---|---|
| `GrapplingHook.prefab` | Hook tip with kinematic RB + projectile component |
| `RopeSegment.prefab` | Single rope link sprite |
| `GrapplingHookAnchor.prefab` | Anchor ring sprite + trigger collider + marker |
| Hook tip sprite | ~8x8 to 16x16 pixel art |
| Rope link sprite | ~4x4 or 4x8 pixel art |
| Anchor ring sprite | Decorative ring/hook point |
