# Stone Golem — Editor Wiring Checklist

One-time Unity setup to make the new action architecture run. The C# is in place and compiles;
nothing below works until the prefab is wired and the animation events are re-pointed.

Prefab: `Assets/Game/Features/Bosses/StoneGolem/StoneGolem.prefab`

## 1. Root GameObject (`StoneGolem`)

- [ ] Confirm `Facing2D` was auto-added (it is required by `StoneGolem`). If not, Add Component → `Facing2D`.
- [ ] Confirm `Rigidbody2D` is present (gravity on, Z-rotation frozen) and `Animator` uses `_StoneGolem` controller.
- [ ] Set `StoneGolem.moveSpeed` (walk speed).
- [ ] Add Component → `StoneGolemAI`.

## 2. `Actions` child container

- [ ] Create empty child GameObject `Actions` under `StoneGolem`.
- [ ] Create three children under it: `MeleeAction`, `HandShootAction`, `BeamShootAction`.

### MeleeAction (on `Actions/MeleeAction`)
- [ ] Add Component → `MeleeAction`.
- [ ] `animator` = root `Animator`.
- [ ] `attackTrigger` = `onMeleeAttack` (default).
- [ ] `meleeDamager` = the `MeleeDamager` child GameObject.
- [ ] `holdDuration` ≈ the Melee clip length.
- [ ] `maxDuration` = safety cap (> holdDuration).

### HandShootAction (on `Actions/HandShootAction`) — `ProjectileShootAction`
- [ ] Add Component → `ProjectileShootAction`.
- [ ] `golem` = root `StoneGolem`.
- [ ] `animator` = root `Animator`.
- [ ] `projectilePrefab` = `HandProjectile.prefab`.
- [ ] `spawnPoint` = `HandSpawnPos`.
- [ ] `shootTrigger` = `onShootHand`.
- [ ] `shootEndTrigger` = `onShootHandEnd`.

### BeamShootAction (on `Actions/BeamShootAction`) — `ProjectileShootAction`
- [ ] Add Component → `ProjectileShootAction`.
- [ ] `golem` = root `StoneGolem`.
- [ ] `animator` = root `Animator`.
- [ ] `projectilePrefab` = `StoneGolemBeam.prefab`.
- [ ] `spawnPoint` = `BeamSpawnPos`.
- [ ] `shootTrigger` = `onCastBeam`.
- [ ] `shootEndTrigger` = leave empty (no beam-end state wired).

## 3. StoneGolemAI (on root)

- [ ] `golem` = root `StoneGolem`.
- [ ] `meleeAction` = `Actions/MeleeAction`.
- [ ] `handShootAction` = `Actions/HandShootAction`.
- [ ] `beamShootAction` = `Actions/BeamShootAction`.
- [ ] `handShootArea` = the `HandShootArea` collider.
- [ ] Tuning: `cadence`, `meleeRange`, `warmupDelay`.

## 4. Re-point animation events (Animation window)

The clips currently call per-action event names. Re-point them to the generic relay on the
controller so the boss's event surface stays fixed.

- [ ] `Melee.anim`: `OpenMeleeDamageWindow` → `OnActionDo`; `CloseMeleeDamageWindow` → `OnActionTearDown`.
- [ ] `ShootHand.anim`: `ShootHand` → `OnActionDo`.
- [ ] `CastBeam.anim`: `StartShootingBeam` → `OnActionDo`.

## 5. Projectile prefabs

Both `HandProjectile.prefab` and `StoneGolemBeam.prefab` currently hold only a sprite + `Facing2D`.

- [ ] Add Component → `StoneGolemProjectile` (this auto-adds `Rigidbody2D`).
- [ ] Set the `Rigidbody2D` to **Kinematic** (gravityScale 0).
- [ ] Add a trigger `Collider2D` sized to the projectile.
- [ ] Add Component → `Damager`; set `targetLayers` to the player layer and the damage value.
- [ ] Set `StoneGolemProjectile.speed` and `travelDistance` (reach + pace).

## 6. HandShootArea

- [ ] Ensure the `HandShootArea` collider is a trigger sized to the zone where the golem should
      throw its hand.

## Debug controls (optional)

- [ ] Add `StoneGolemDebugControls` to the golem root for an in-inspector test panel (play mode):
      a button per action under the golem + Move Left / Move Right / Stop. Buttons are enabled
      only while the golem is free; the first command suspends `StoneGolemAI` (press "Resume AI"
      to hand control back). Remove the component when done.

## Smoke test (Play mode)

- [ ] Golem walks toward the player when idle and out of range.
- [ ] Melee: damage window opens only on the strike frame, closes after; the next action waits until the swing finishes.
- [ ] Hand: spawns on the spawn frame, flies out, returns, despawns; golem frees up.
- [ ] Beam: fires and completes.
- [ ] Kill the golem mid-action → action cancels cleanly (no lingering damager / projectile).
