# CPU Performance Analysis — `Assets/Game`

_Date: 2026-05-11 · Branch: `render-optimization`_

This doc lists CPU-side optimization opportunities found in `Assets/Game`, ranked by impact. The primary focus is **object pooling candidates**; other GC / per-frame hotspots are listed afterward.

No code is changed by this doc — it's an analysis only.

---

## 1. Headline Findings

- **No general object pool exists yet.** `SpawnerService` (`Assets/Game/Core/Services/SpawnerService.cs`) is a thin `Instantiate` wrapper. Every projectile, VFX, and effect today is a fresh `Instantiate` + later `Destroy`.
- **The codebase already has a pooling vocabulary.** `AudioService` pools `AudioSource`s, and `ListPool<T>` (`Assets/Game/Core/Audio/AudioService.cs:687`) pools generic `List<T>`s. New pooling work should follow the same shape.
- **Animator hashes and `Physics2D` non-alloc calls are already used consistently.** Those are not where the wins are.
- **Biggest single payoff:** pooling `LinearProjectile` and the central `SpawnerService.SpawnVfx(...)` path covers a large fraction of in-combat allocations.

### Priority Table

| # | Item | Impact | Risk | Effort |
|---|------|--------|------|--------|
| 1 | Pool `LinearProjectile` (shared by Cannon / Seashell / Skullflame / GiantFly) | High | Low | M |
| 2 | Pool VFX through `SpawnerService.SpawnVfx(...)` | High | Low | M |
| 3 | Pool `SpectralSword` (boss patterns) | High | Low | S |
| 4 | Pool `SpinningSword` + embedded sword | Med | Low | S |
| 5 | Pool `GrapplingHookRope` segments | Med | Low | S |
| 6 | Cache `WaitForSeconds` instances across coroutines | Med | Very low | S |
| 7 | Fix per-frame allocations in `DragAbility` and `VengefulSpiritAI` | Med | Low | S |
| 8 | Cache `Camera.main` in a few places | Low | Very low | XS |
| 9 | `InventoryModel` list → dictionary (existing TODO) | Low | Low | S |

Suggested order of work mirrors the table.

---

## 2. Object Pooling Candidates

Grouped by category. For each entry: where it's spawned, why it matters.

### 2.1 HIGH priority — spawned per attack / per pattern

**Projectiles**

- **`LinearProjectile`** — `Assets/Game/Features/Characters/_Shared/LinearProjectile.cs:160,164` (`Destroy` on timeout / on hit)
  Shared base used by Cannon, Seashell, Skullflame, GiantFly shooters. This is the single highest-traffic projectile type in the project; pooling it once benefits four shooter families.
- **`SpinningSword`** — `Assets/Game/Features/Characters/Hero/Projectiles/SpinningSword.cs:39,42`
  Destroyed on hit; spawns an embedded sword via `embeddedSwordSpawner.SpawnInstance()`. Pool both.
- **`SpectralSword`** — spawn: `Assets/Game/Features/Bosses/VengefulSpirit/SpectralSwords/SpectralSwordSpawnAnchor.cs:80`, destroy: `SpectralSword.cs:89,98,106`
  Many spawned per boss pattern entry inside a coroutine — clean pooling target.
- **Cannon / Seashell / Totem projectiles** — `Assets/Game/Features/Hazards/ShootingTraps/Cannon/CannonController.cs`, `…/Seashell/SeashellController.cs`, `…/Totem/Projectiles/Skullflame/SkullflameController.cs`, `…/Totem/Projectiles/GiantFly/GiantFlyController.cs`.
  Most route through `LinearProjectile` — pooling the base covers these.

**VFX / Particles**

- **`SpawnerService.SpawnVfx(...)`** — `Assets/Game/Core/Services/SpawnerService.cs:64,68,76`
  Central choke point for particle-system effects. Pool here once and every caller benefits without callsite changes.
- **Boss cast particles** — `Assets/Game/Features/Bosses/VengefulSpirit/AI/Patterns/SpawnShieldPattern.cs:79`
  Currently a direct `Instantiate`; route through the pooled `SpawnVfx`.

### 2.2 MEDIUM priority

- **`GrapplingHookRope` segments** — `Assets/Game/Features/Characters/Hero/GrapplingHook/GrapplingHookRope.cs:45` (spawn loop), `:186,191` (destroy).
  Burst-spawned per hook use. Either pool the segments or reuse the same rope instance across hook uses.
- **Info bubbles** — `Assets/Game/Core/Services/SpawnerService.cs:81`. Predictable lifetime; pool with a small max size.
- **Debris** — `Assets/Game/Core/Components/Effects/DebrisController.cs:78,92`. Destroyed after animation; trivial to pool.

### 2.3 LOW priority — UI (event-driven, low frequency)

- Dialog choice buttons — `Assets/Game/UI/Dialog/DialogPanel.cs:171`
- Inventory items — `Assets/Game/UI/Inventory/BackpackPanel.cs:85`
- Shop items — `Assets/Game/UI/ShopInventory/ShopInventory.cs:89`
- Stat shop items — `Assets/Game/UI/StatShop/StatShop.cs:73`
- Menu windows — `Assets/Game/Core/Services/Scene/MenuManager.cs:290`

These don't justify pool bookkeeping unless panels are reopened frequently.

### 2.4 Don't pool

One-shot / gameplay-meaningful instances whose lifetime is owned by another system:

- Parrot summon — `Assets/Game/Features/Characters/Parrot/ParrotDeployStrategy.cs:33`
- Protection aura — `Assets/Game/Features/Characters/Hero/ItemUse/ProtectionMaskStrategy.cs:52`
- Boss shield — `Assets/Game/Features/Bosses/VengefulSpirit/VengefulSpirit.cs:1182`
- Collectibles — `Assets/Game/Core/Components/Collectables/CollectableBase.cs:122,124` (destroy on pickup is meaningful and rare)

---

## 3. Recommended Pooling Approach

Suggestions, not prescriptions:

- **Use `UnityEngine.Pool.ObjectPool<T>`** (built into modern Unity). It supports `actionOnGet` / `actionOnRelease` / `actionOnDestroy` callbacks, max-size capping, and is well-tested. Avoid a custom pool unless something specific is missing.
- **Extend `SpawnerService`** with `SpawnPooled(prefab, …)` and `Despawn(instance)` methods. Callsites change minimally; the pool lookup is keyed by prefab.
- **Define a reset contract.** Pooled objects must reset their state on `OnEnable` (or via an `IPoolable.OnSpawn() / OnDespawn()` interface): clear `Rigidbody2D` velocity, re-enable colliders, reset trail/particle systems, clear damage callbacks.
- **Pool category, not lifetime.** Put a single particle pool for `ParticleSystem` prefabs behind `SpawnVfx`, keyed by prefab reference, with a built-in "auto-despawn when finished" component attached on first spawn.
- **Reference patterns already in repo:** `ListPool<T>` at `Assets/Game/Core/Audio/AudioService.cs:687`, pooled `AudioSource` queue in `AudioService` itself.

---

## 4. Other CPU / GC Hotspots

### 4.1 Per-frame allocations

- **`DragAbility.CountBarrelsAboveSorted()`** — `Assets/Game/Features/Characters/Hero/Abilities/DragAbility.cs:236,238,254`
  Allocates a fresh `HashSet`, a `Queue`, and calls `.ToList()`. Promote them to fields and `Clear()` before reuse, or borrow from `ListPool<T>`.
- **`VengefulSpiritAI.GetNextPattern()`** — `Assets/Game/Features/Bosses/VengefulSpirit/VengefulSpiritAI.cs:253,254`
  `new List<BossPatternSlot>()` on every selection. Reuse a field-level list.
- **`MultiRayCaster`** — `Assets/Game/Core/Utils/MultiRayCaster.cs:142`
  `.ToList()` on a `HashSet`. Either expose the `HashSet` directly or reuse a cached `List`.

### 4.2 `new WaitForSeconds(...)` in coroutines

14+ sites allocate a fresh `WaitForSeconds` every time a coroutine yields. Cache `static readonly WaitForSeconds` per common delay or introduce a small `WaitCache` helper.

Highest-traffic sites:

- `Assets/Game/Features/Bosses/VengefulSpirit/SpectralSwords/SpectralSwordSpawnAnchor.cs:68`
- `Assets/Game/Features/Characters/Sharky/SharkyAI.cs:111,130,141,166,172`
- `Assets/Game/Features/Bosses/VengefulSpirit/Intro/BossIntroCutscene.cs:117,125,128,143`
- `Assets/Game/Core/Components/Behavior/AutoTrigger.cs:109,126,140`
- `Assets/Game/UI/Dialog/DialogPanel.cs:240` (`WaitForSecondsRealtime`)
- `Assets/Game/Features/Characters/Hero/PlayerController.cs:704,752`
- `Assets/Game/Features/Bosses/VengefulSpirit/Teleport/SpiritTeleporter.cs:136`
- `Assets/Game/Features/Bosses/VengefulSpirit/VengefulSpirit.cs:491`
- `Assets/Game/Features/Effects/InfoBubble/InfoBubble.cs:30`
- `Assets/Game/Core/Components/Interaction/Switch.cs:85`
- `Assets/Game/UI/MainMenu/MainMenuLauncher.cs:59`

### 4.3 `Camera.main` access

Each call does a tag lookup; repeated access in `Update`/`OnDrawGizmos` adds up.

- `Assets/Game/Features/Effects/DeathScreen/DeathScreenEffect.cs:208,287,319` — three calls, cache once.
- `Assets/Game/Core/Components/Camera/ParallaxLayer.cs:52,111,112` — cache the camera reference.

(`AudioListenerFollow2D.cs:17,18` already caches it — model after that.)

### 4.4 Inventory lookup

- `Assets/Game/Core/Models/Inventory/InventoryModel.cs:91` — existing `// TODO: [BG] Optimize. Probably we should use dictionary`. Linear scan over the list per lookup. Convert to `Dictionary<ItemId, …>` if profiling confirms the call frequency.

### 4.5 Material instancing

- `Assets/Game/Features/Effects/DeathScreen/DeathScreenEffect.cs:268` — `irisImage.material = irisMat` creates a per-instance material clone. Verify whether `sharedMaterial` is acceptable, or pool the cloned material.

---

## 5. Already Good — Don't Re-Audit

So nobody wastes time here:

- **Animator parameters** use `static readonly int` `StringToHash` consistently across all controllers (e.g. `PlayerController.cs:23-30`, `PinkyController.cs:14-20`, `VengefulSpirit.cs:20-24`, `ChestController.cs:8-10`).
- **`Physics2D` queries** predominantly use `NonAlloc` variants with pre-allocated buffers (`MultiRayCaster`, `CheckCircleOverlap`, `GiantFlyController`, `SkullflameController`, `BlinkAttackPattern`, `GrapplingHookAbility`).
- **AudioService** pools `AudioSource`s with configurable bounds and ships `ListPool<T>`.
- **`TriggerInteractionProvider` / `DragAbility`** reuse a `cachedCandidate` adapter to avoid per-frame allocations during interaction resolution.

---

## 6. Suggested Order of Work

1. **Infra:** add `ObjectPool<T>` plumbing inside `SpawnerService` (`SpawnPooled` / `Despawn`) and an `IPoolable` reset contract.
2. **Pool `LinearProjectile`** — covers Cannon, Seashell, Skullflame, GiantFly in one change.
3. **Route VFX through pooled `SpawnVfx`** — biggest gameplay-wide effect with the least callsite churn.
4. **Pool `SpectralSword`** — easy to verify inside `BossSkeletonRoom`.
5. **Pool `SpinningSword`** (+ embedded sword).
6. **Pool `GrapplingHookRope` segments.**
7. **Cache `WaitForSeconds`** across the call sites in §4.2 (mechanical refactor).
8. **Fix `DragAbility` / `VengefulSpiritAI` per-frame allocations.**
9. **Cache `Camera.main`** in the two locations in §4.3.
10. **Inventory dictionary** if profiling confirms it's a hot path.

---

## 7. Notes for Verification

Each pooling change should be verified by:

- Playing through `BossSkeletonRoom` (boss patterns exercise spectral swords + cast VFX).
- Triggering all four shooter trap families (Cannon, Seashell, Skullflame, GiantFly).
- Using the grappling hook several times in succession.
- Watching the memory profiler (already added on this branch) for `Instantiate`-driven allocations dropping to ~zero after warmup.