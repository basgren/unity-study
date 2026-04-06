# 06 - Multi-Layer Parallax

## Goal

Implement a multi-layered parallax scrolling system for background/foreground layers:

1. Each parallax layer is a separate parent GameObject with one component.
2. Parallax speed is configurable per layer; by default it is derived from the layer's Z position — the farther from Z = 0, the slower the parallax.
3. Movement is driven by camera position (Cinemachine).
4. An editor gizmo shows the minimum required image size for the layer and overlays the camera confiner shape, so it is easy to see what area will be visible on screen.

## Current State

`Assets/Game/Core/Components/Camera/ParallaxScroller.cs` exists but is minimal:

- Namespace is `Core.Components.Camera` (should be `Game.Core.Components.Camera`).
- Single field `Vector2 parallaxMultiplier` — manually typed, no Z-based default.
- No gizmo, no confiner awareness, comments in Russian.
- Uses `CinemachineCore.CameraUpdatedEvent` — the correct timing hook (after Cinemachine finishes updating the camera, before the next frame render).

## Approach A — Extend ParallaxScroller in Place

Rename the namespace, add Z-auto-calculation, add the gizmo, clean up comments.

**Pros**
- Minimal churn; existing scene wiring for any objects already using `ParallaxScroller` is preserved.

**Cons**
- Changing the namespace forces every scene that serialised a `ParallaxScroller` reference to re-link anyway (Unity stores component type by namespace-qualified name).
- The existing code is a weak starting point for a well-documented, complete component.
- The field name `parallaxMultiplier` conflicts with the intended mental model (it is more naturally called `speed`).

**Verdict:** Not recommended. The namespace change invalidates the "no churn" advantage.

## Approach B — New `ParallaxLayer` Component (Recommended)

Delete `ParallaxScroller`, create `ParallaxLayer` in the correct namespace with the full feature set.

### Component API

```csharp
// Assets/Game/Core/Components/Camera/ParallaxLayer.cs
namespace Game.Core.Components.Camera {
    public class ParallaxLayer : MonoBehaviour {
        [SerializeField] private bool overrideSpeed = false;

        /// Used only when overrideSpeed is true.
        [SerializeField] private Vector2 speed = Vector2.one;

        /// Used by the editor gizmo to draw required-size visualization.
        [SerializeField] private PolygonCollider2D confiner;
    }
}
```

### Speed Formula (Z-Based Default)

When `overrideSpeed` is false, the effective speed is calculated from the layer's Z position once in `Awake`:

```
effectiveSpeed.x = effectiveSpeed.y = 1f / (Mathf.Abs(transform.position.z) + 1f)
```

| Z | Factor |
|---|--------|
| 0 | 1.00 — moves with camera, no visible parallax |
| −1 | 0.50 — half speed |
| −3 | 0.25 — quarter speed |
| −9 | 0.10 — very slow (distant sky) |
| +1 | 0.50 — foreground, moves faster than background |

X and Y use the same formula independently. When `overrideSpeed` is true, the `speed` field is used directly, allowing asymmetric horizontal/vertical control (e.g. `speed = (0.2, 0)` for a layer that scrolls horizontally only).

### Runtime Behaviour

```csharp
private void Awake() {
    brain = Camera.main.GetComponent<CinemachineBrain>();
    cam = brain.transform;
    // Snap to camera start position, preserve Z.
    transform.position = new Vector3(cam.position.x, cam.position.y, transform.position.z);
    startPos = transform.position;
    camStartPos = cam.position;
    if (!overrideSpeed) {
        var f = 1f / (Mathf.Abs(transform.position.z) + 1f);
        effectiveSpeed = new Vector2(f, f);
    } else {
        effectiveSpeed = speed;
    }
    CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
}

private void OnCameraUpdated(CinemachineBrain b) {
    var delta = cam.position - camStartPos;
    transform.position = startPos + new Vector3(delta.x * effectiveSpeed.x, delta.y * effectiveSpeed.y, 0f);
}
```

`CinemachineCore.CameraUpdatedEvent` fires after Cinemachine finishes updating the camera each frame, which avoids a one-frame lag. `LateUpdate` is not used because Cinemachine may run after `LateUpdate` depending on the execution order.

### Pros / Cons

**Pros**
- Correct namespace and conventions from the start.
- Full feature set: Z-auto, manual override, gizmo.
- Self-contained component, no manager needed.

**Cons**
- Any scene objects that already had `ParallaxScroller` need to be re-wired (add `ParallaxLayer`, configure confiner reference, remove old component). Low cost for a small project.

## Approach C — `ParallaxSystem` Manager + `ParallaxLayer` Children

One manager component on a root GameObject fetches the camera once and broadcasts the camera delta to child `ParallaxLayer` components each frame.

**Pros**
- Single camera lookup.
- Central enable/disable.

**Cons**
- More components and hierarchy setup for no functional gain at this project scale.
- Children depend on the parent being present — fragile to scene restructuring.

**Verdict:** Over-engineered for the use case. Approach B is sufficient.

## Recommended Design: Approach B

### Gizmo Specification

The gizmo is drawn in `OnDrawGizmosSelected` (visible only when the layer object is selected in the editor). It requires a `PolygonCollider2D confiner` reference wired in the inspector. If the reference is missing, the gizmo is skipped.

Effective speed for gizmo calculations = same formula as runtime (`overrideSpeed` is respected).

#### 1. Confiner Polygon (yellow)

Draw the PolygonCollider2D path as a closed polyline to show the total travel bounds of the camera.

```csharp
Gizmos.color = new Color(1f, 0.9f, 0f, 0.6f);
var points = confiner.GetPath(0);
for (int i = 0; i < points.Length; i++) {
    var a = confiner.transform.TransformPoint(points[i]);
    var b = confiner.transform.TransformPoint(points[(i + 1) % points.Length]);
    Gizmos.DrawLine(a, b);
}
```

#### 2. Required Image Extent (white)

The minimum rectangle the layer's sprite/image must cover so no gap is ever visible as the camera moves across the full confiner.

```
confinerBounds = confiner.bounds   // world-space AABB
vpW = 2 * cam.orthographicSize * cam.aspect
vpH = 2 * cam.orthographicSize

requiredW = vpW + confinerBounds.size.x * (1f - effectiveSpeed.x)
requiredH = vpH + confinerBounds.size.y * (1f - effectiveSpeed.y)
```

The rectangle is centred on the layer's `transform.position` (XY):

```csharp
Gizmos.color = Color.white;
Gizmos.DrawWireCube(
    new Vector3(transform.position.x, transform.position.y, transform.position.z),
    new Vector3(requiredW, requiredH, 0f)
);
```

**Derivation:** When the camera is at position `cx`, the layer centre is at `startX + (cx − cx₀) × speedX`. The camera shows `[layerCx − vpW/2, layerCx + vpW/2]`. Over the full confiner range `[cxMin, cxMax]`, the layer is queried across `[(cxMin − cx₀) × speedX − vpW/2, (cxMax − cx₀) × speedX + vpW/2]`, giving total width `vpW + (cxMax − cxMin) × speedX − (cxMin − cx₀) × speedX + ... = vpW + confinedRangeX × (1 − speedX)`.

Wait — corrected derivation: the union of all visible intervals across all camera positions has width `vpW + confinedRangeX × (1 − speedX)` because the layer travels `confinedRangeX × speedX` while the viewport sweeps `confinedRangeX`. The gap is `confinedRangeX × (1 − speedX)`.

#### 3. Viewport at Confiner Extremes (cyan, semi-transparent)

Draw the camera viewport rectangle at the two diagonal extremes of the confiner (min corner and max corner). This shows the exact view at the scroll limits.

```csharp
Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
var camMin = (Vector3)confiner.bounds.min;
var camMax = (Vector3)confiner.bounds.max;
var vpSize = new Vector3(vpW, vpH, 0f);
// Layer position when camera is at confiner min.
var layerAtMin = startPos + (camMin - camStartPos) * effectiveSpeed;
// Layer position when camera is at confiner max.
var layerAtMax = startPos + (camMax - camStartPos) * effectiveSpeed;
// Camera viewport in world space at those camera positions.
Gizmos.DrawWireCube(new Vector3(camMin.x, camMin.y, transform.position.z), vpSize);
Gizmos.DrawWireCube(new Vector3(camMax.x, camMax.y, transform.position.z), vpSize);
```

*(Note: the viewport is drawn at camera world position, not layer position, because that is what the player sees.)*

## Summary of File Changes

### New Files

| File | Purpose |
|---|---|
| `Assets/Game/Core/Components/Camera/ParallaxLayer.cs` | New parallax layer component |

### Deleted Files

| File | Reason |
|---|---|
| `Assets/Game/Core/Components/Camera/ParallaxScroller.cs` | Superseded by ParallaxLayer; wrong namespace |

### Scene Changes

For each scene that has a GameObject with `ParallaxScroller`:
1. Remove the `ParallaxScroller` component.
2. Add `ParallaxLayer`.
3. Wire the `Confiner` reference to the scene's `CameraConfiner` object.
4. If the old `parallaxMultiplier` was customised, enable `overrideSpeed` and enter the old value as `speed`.

## Editor Steps After Implementation

1. Select a background layer object in a scene → gizmo should draw yellow confiner polygon, white required-size box, cyan viewport rectangles at extremes.
2. Enter Play mode, walk player to the edges of the level → verify the background image fills the screen without gaps.
3. Try `overrideSpeed = true` with `speed = (0.2, 0)` → vertical speed should lock to zero.
