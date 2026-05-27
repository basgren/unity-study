# Stone Golem — Unity Wiring Checklist

Manual Editor wiring for the Stone Golem. Code is built; this is what must be set in prefabs,
the scene, and the Animator. For the *why* / behaviour, see
[`boss-stone-golem.md`](boss-stone-golem.md).

Convention: every `EnemyAction`'s `maxDuration` is a safety cap that force-completes the action —
**set it longer than the action's full duration, or 0 to disable.** Long maneuvers are flagged ⚠️.

## Golem host
- [+] `Damageable` on the golem: `maxHealth = 100`.
- [ ] **`ActionRefs`: only `melee` + `handShoot` are bound; `beamShoot`/`groundHit`/`laserCross`/
      `stoneWave` are NULL** → those 4 attacks never fire from the AI. Run the `StoneGolem` gear-menu
      **"Auto-bind"** (fills empty slots from the child components), or drag them in. (Adding new
      ActionRefs fields in code doesn't re-bind existing prefab instances — hence the gap.)
- [+] `StoneGolemAI` pacing/melee: `phase1Cadence 1.5`, `phase2Cadence 1.0`,
      `phaseShiftHealthFraction 0.5`, `meleeCooldown 1.5`, `chaseTimeout`, `flyDuration 2`,
      `levelTolerance`.
- [ ] **`StoneGolemAI.meleeTriggerArea`**: drag the golem's **`MeleeTriggerArea`** child collider here.
      The golem decides it can melee (both the reactive counter and the chase stop) by testing whether
      the player overlaps this trigger — it replaces the old `meleeRange` / `reactiveRange` distances.
      Size/offset the box to the desired reach; it flips with the golem's facing automatically.
- [+] `StoneGolemAI.phase1Pool` / `phase2Pool`: populated (P1 Melee/Hand/Beam/GroundHit;
      P2 + LaserCross/StoneWave). NOTE: the StoneWave entry is inert until its component exists (below).

## Arena anchors (phase-1 movement)
- [+] **Not done — `anchors[]` is empty, no markers placed.** Until then there's no level-matching or
      flying (Ground Hit just slams in place).
- [+] Place empty GameObjects with `StoneGolemAnchorPoint`; set each `level` — **Level1** (ground /
      ground-hit + take-off, ~3 in the upper room) and **Level2** (platform landing spots, ~4). Add
      **Level1** points in the lower (phase-2) room too, for Ground Hit there.
- [+] Drag them all into `StoneGolemAI.anchors[]`. The AI matches the player's level via the lowest
      Level2 anchor's Y, and picks the nearest anchor by **2D distance to the player**.
- **Upper vs lower room:** they're told apart by proximity (the player is always in the active room,
  the rooms are stacked, so the same-room anchor is nearest) — no per-anchor room tag needed. The AI
  also **skips inactive anchors**, so a clean option is to parent the upper-room anchors to the
  platforms/floor that the transition destroys: they auto-disable in phase 2, leaving only the
  lower-room (Level1) anchors. See [`boss-stone-golem.md`](boss-stone-golem.md) §"Arena & Movement".

## Navigation graph (anchor connections) — REQUIRED for movement
The golem now travels **only along authored edges** (no diagonal flies). On each `StoneGolemAnchorPoint`,
fill `connections[]` (`target` + `type`: Move / Jump / HorzJump). Edges auto-mirror, so wire each link
**once**; gizmos draw them (white=move, green=jump, cyan=horzJump). Per the doc's 7 phase-1 nodes:
- [ ] p1.1–p1.2 **Move**, p1.2–p1.3 **Move** (ground)
- [ ] p1.1–p2.1 **Jump**, p1.2–p2.2 **Jump**, p1.2–p2.3 **Jump**, p1.3–p2.4 **Jump** (level changes)
- [ ] p2.1–p2.2 **Move** (left platform), p2.3–p2.4 **Move** (right platform)
- [ ] p2.2–p2.3 **HorzJump** (across the gap)
- [ ] **Phase-2 room** Level1 anchors: add **Move** links between neighbours (optional — no links ⇒
      the golem falls back to a direct fly there, with a console warning).
- [ ] Tune `StoneGolemAI`: `jumpDuration`/`jumpArcHeight`, `horzJumpDuration`/`horzJumpArcHeight`,
      `anchorArriveThreshold`, `moveEdgeTimeout`. `flyDuration` is now only the no-route fallback.
- **Note:** every anchor the golem may stand on needs at least one connection, or it can't path out
  (it will fall back to flying). Watch the console for "no navigation route" warnings.

## Attacks 1–3 (Melee / Hand / Beam)
- [+] **Melee**: animator `onMeleeAttack` trigger; the melee clip carries `OnActionDo` (open) +
      `OnActionTearDown` (close) events; `meleeDamager` child wired.
- [+] **Hand**: `HandProjectile.prefab` — kinematic `Rigidbody2D`, set `groundLayerMask`, `speed`,
      `returnSpeed`, `maxRange`, `returnDelay`. Action: `golem`, `animator`, `projectilePrefab`,
      `spawnPoint`. The `ShootHand` clip fires `OnActionDo` on the throw frame.
- [+] **Beam**: `StoneGolemBeam.prefab` — `collideWithGround = true`, `groundLayerMask`, sweep fields
      (`startAngleDeg -45`, `sweepArcDeg 90`, `sweepTime`). Wire the `beamShoot` slot + `spawnPoint`;
      `CastBeam` clip fires `OnActionDo` on the cast frame.

## Attack 4 — Ground Hit
- [+] `FallingStone.prefab`: Dynamic `Rigidbody2D` (~1 gravity), `FallingStone` component with
      `groundLayerMask`, optional impact effect.
- [+] `GroundHitAction`: `golem`, `animator`, `fallingStonePrefab`, **`spawnArea`** → a room-placed
      disabled `BoxCollider2D` spanning the floor width (NOT a golem child); stone/motion fields.
- [+] Animator: `isImmune` bool + ball-up / un-ball states.

## Attack 5 — Transition cutscene  (component on golem; golem/animator wired)
- [+] **`breakObjects[]` is EMPTY → nothing breaks, nobody falls. Must wire** the floor tilemap layer
      + platforms + hook anchor. ANSWER: initialized on scene, as it refers objects that are not
      children of the golem - they are parts of the scene.
- [+] **`breakPoint` is null** (level-1 Transform) — currently breaks in place.
- [+] Wired into `StoneGolemAI.phaseTransitionAction`.
- [ ] **`onFloorBroken` → bigger camera confiner (phase 2).** Driven through `GolemSceneDirector` +
      `CameraConfinerSelector` (see the **Encounter direction** section below). Wire
      `StoneGolemGroundBreakAction.onFloorBroken` → `GolemSceneDirector.OnFloorBroken()`.
      (The old `SwapCameraConfinerShape.Trigger()` wiring is **deprecated** — it only supports one
      expanded shape and can't also switch to the post-defeat `CameraConfinerFinal`.)
- [ ] `breakEffectPrefab` (+ SFX/shake on `onFloorBroken`) — still optional/unset.
- [+] ⚠️ `maxDuration` is **5** — too short (~9 s cutscene). Set ≈ 12 s or 0.

## Attack 6 — Laser Cross  (component on golem; golem/animator/beamPrefab wired)
- [+] **`centerPoint` is null** → won't fly to centre, spawns the cross where it stands. Wire a
      room-centre Transform. ANSWER: added right on scene. it should not be on prefab, as it's scene-specific.
- [+] ⚠️ `maxDuration` is **5** — too short (rotation alone is 6 s). Set ≈ 12 s or 0.

## Attack 7 — Stone Wave
- [+] **No `StoneWaveAction` component on the golem yet → Stone Wave never fires** (phase2Pool entry
      resolves to null). Add the child + auto-bind.
- [+] `StoneWaveAction`: `golem`, `animator`, `spikePrefab` → `StoneSpikes`, `groundLayerMask`.
- [+] Animator: `isGlowing` bool + glow/flash state.
- [+] `StoneSpikes` prefab: set its sprite animator **`destroyOnComplete = true`** (else spikes linger).
- [+] ⚠️ `maxDuration` ≈ 15–20 s or 0.

## Intro / engagement  (simplified: offscreen drop-in, no wall)
- [ ] Place the golem **offscreen above** the arena. Add a `landingPoint` Transform on the arena floor
      beneath it.
- [ ] Add `StoneGolemIntro` (on the golem or a scene object): wire `golem`, `golemAI`, `animator`,
      `landingPoint`; tune `fallTime` (~0.8 s descent) + `revealHold`.
- [ ] On Start the intro holds the golem in the **immune (ball)** state with gravity + `StoneGolemAI`
      disabled. `Begin()` drops it to `landingPoint`, holds, rises (immune off), engages `G.BossFight`,
      enables the AI.
- [ ] Wire the **fake-skull pickup's UnityEvent → `GolemSceneDirector.StartFight()`** (the director
      forwards to `StoneGolemIntro.Begin()`; route start through the director, not the intro directly).
      The skull's own break-apart on pickup is separate.
- [ ] ⚠️ **Boss must stand still until the skull is picked.** The inert hold is `StoneGolemIntro.Start`
      (disables `StoneGolemAI`). If the golem starts moving on scene load, the culprit is
      `StoneGolemDebugControls.startWithAi = true` — a **debug bypass** that force-enables the AI at
      Start. Set it **false** (or remove `StoneGolemDebugControls`) in the shipping scene.

## Encounter direction (`GolemSceneDirector`)
Central milestone + persistence layer for the room. It does **not** run the fight, the intro, or the
phase-2 cutscene (those keep their own logic); it reacts to two events and drives the confiner, the
sanctuary door, and the win flag. See `docs/system/boss-stone-golem.md` §"General scene direction".

- [+] Add `GolemSceneDirector` to a scene object (e.g. an `Encounter` GameObject). Wire `golem`,
      `golemAI`, `intro`, optional `fightStartTrigger` (the SkullFake/its trigger), `confinerSelector`,
      and `sanctuaryDoor` (the `StoneDoor_Sanctuary` `SwitchableBase`). Set `bigConfinerId` /
      `finalConfinerId` to match the selector's authored state ids (defaults `Big` / `Final`).
- [+] **Camera confiner — `CameraConfinerSelector`** (replaces the deprecated `SwapCameraConfinerShape`):
      1. Author three bounding `Collider2D` shapes: the existing small `CameraConfiner`,
         `CameraConfinerBig` (upper + lower rooms), `CameraConfinerFinal` (post-defeat).
      2. Add `CameraConfinerSelector` (e.g. on the same object as the director). Set `confiner` = the
         **active gameplay** vcam's `CinemachineConfiner2D`. Add `states`: `{ id:"Big", shape:Big }`,
         `{ id:"Final", shape:Final }` (the small one is the vcam's authored default — no entry needed).
      3. `StoneGolemGroundBreakAction.onFloorBroken` → `GolemSceneDirector.OnFloorBroken()` (→ "Big").
- [+] **Boss defeat** → `golem.Damageable.onDeath` → `GolemSceneDirector.OnBossDefeated()` (opens the
      sanctuary door, switches to "Final"). Wire the no-arg method via the static dropdown.
- [+] **Persistence:** put `GolemEncounterStateSaver` + a `StateRoot` (`Tier = Persistent`) on the
      **same** GameObject as the director. The `defeated` bool is the single saved fact; on re-entry
      `ApplyDefeatedInstant` opens the door, applies `CameraConfinerFinal` immediately, and removes the
      boss — no refight. The sanctuary door is derived from this flag, so it needs **no** separate
      `SwitchableStateSaver`.
- [+] **Do NOT** persist boss HP / the broken floor / the "Big" confiner — so dying reloads the scene
      and restarts the fight from phase 1. (Assumes death respawns via **scene reload**; if respawn only
      repositions the player, the director needs an explicit `ResetEncounter` instead.)
