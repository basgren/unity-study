# 2026-04-30 - Vengeful Spirit Two-Phase Behavior

## Goal

Define how the Vengeful Spirit boss should behave during a fight, split
into two phases. Phase 1 should teach the moveset and stay readable; phase 2
should feel harder by combining the same actions more aggressively. **No
code in this doc — just behavior.**

The boss already exposes the building blocks the AI needs:
- Movement (`XDirection`, `YDirection`)
- One-shot actions: `Attack` (melee thrust), `SpawnShield`, `CastSwords`,
  `Teleport`
- Named anchor lookups: `GetSwordAnchor(name)`, `GetTeleportAnchor(name)`

A future AI subclass of `VengefulSpiritControlSource` will drive these.
This doc is the design brief that script will follow.

## Phase Trigger

- **Phase 1 → Phase 2:** when `Damageable.Health <= maxHealth * 0.5f`.
- One-way: phase 2 never returns to phase 1.
- The transition itself is *visible* — the boss is forced into one
  **phase-shift beat**:
  1. Forced teleport to the `Center` anchor (regardless of where the boss
     was).
  2. Immediately on reappear, fire one full sword wave from that anchor
     (`Wave_Stage2` pattern).
  3. Then phase 2 begins normally.

This guarantees the player sees a clear "the fight just changed" moment
instead of a silent stat tweak.

## Phase 1 — "Pressuring but readable"

Teach the moveset; keep windows generous.

- **Cadence.** ~2 s minimum between actions. One action per beat — never
  combos.
- **Movement between beats.** Slow horizontal drift toward the player at
  `moveSpeed`. Boss faces the player. No vertical hunting.
- **Reactivity.** None. Once the boss has chosen an action it commits;
  player attacks don't interrupt.
- **Action picker** (weighted random at each beat):

  | Weight | Pick | Notes |
  |---|---|---|
  | 50% | **Reposition + Slash** | Drift toward player for ~0.5 s, then `Attack` thrust. |
  | 25% | **Standoff sword wave** | `CastSwords` from the anchor on the boss's current side. Uses `Wave_Stage1` (sparse, single-direction). |
  | 15% | **Escape teleport** | Only fires when the player has been within ~3 m of the boss for >1 s. Picks the anchor *farthest from the player*. |
  | 10% | **SpawnShield** | Only when boss HP < 80% **and** no shield currently alive. Acts as a "respect my space" beat — boss does nothing else for the shield's lifetime. |

## Phase 2 — "Aggressive and combinatorial"

Same actions, denser sequencing, less recovery.

- **Cadence.** ~1 s minimum between beats. Picks now select **combos** of
  2-3 beats instead of single actions.
- **Movement between beats.** Active pursuit on both axes. The boss tries
  to stay slightly *above* the player so swords from above land naturally.
- **Reactivity.** If the player is within ~1 m **and** has hit the boss in
  the last 0.3 s, the boss interrupts its drift with a reactive teleport
  to the anchor farthest from the player. Cap: at most one reactive
  teleport per 4 s so the fight can't soft-lock.
- **Combo picker** (weighted random at each combo decision):

  | Weight | Combo | Beats |
  |---|---|---|
  | 35% | **Teleport-strike** | (1) Teleport to the anchor *behind* the player. (2) `Attack` thrust the moment the fade-in ends. The teleport's ~1 s fade-in is the player's only react window. |
  | 30% | **Sword wall** | (1) `CastSwords` from a chosen anchor. (2) After ~0.5 s, second `CastSwords` from a different anchor with mirrored direction. Uses `Wave_Stage2` (denser). |
  | 20% | **Shield bash** | (1) `SpawnShield`. (2) Close distance to the player. (3) `Attack` thrust *through* the shield window so the player has to commit to a side. |
  | 15% | **Triple teleport** | (1)(2)(3) Three teleports in a row to scattered anchors. No other action — pure harassment / repositioning to throw off the player's read. |

## Anchor Layout (suggested)

Author-defined names — the AI references these by string, so the room can
ship whichever subset feels right. The brief assumes this set:

- `Center` — used by the forced phase-shift beat.
- `LeftHigh`, `RightHigh` — high cast points for sword waves.
- `LeftLow`, `RightLow` — low teleport spots for melee combos.

Selection rules used by the AI when picking among them:
- *closest to side* — boss's current screen side.
- *farthest from player* — for escape / reactive teleports.
- *behind player* — anchor whose X is on the opposite side of the player
  from the boss; for Teleport-strike.

## Cross-Cutting Rules

- The AI never emits an action flag while the boss is mid-action. The
  boss controller already gates `isAttacking` / `isCasting` /
  `isTeleporting` defensively, but the AI should also poll those flags so
  it doesn't waste a one-shot input frame.
- Movement input keeps flowing even during teleport (the controller
  preserves and re-applies velocity). The AI may emit a movement
  direction during the hidden window so the boss "drifts" out of the
  fade-in.
- Boss never reacts to its own death — the controller's existing
  death-cleanup tears down all in-flight actions.

## Tuning Knobs (for the implementer to expose)

| Knob | Phase 1 | Phase 2 |
|---|---|---|
| Idle cadence | ~2.0 s | ~1.0 s |
| Combo length | 1 beat | 2-3 beats |
| Sword pattern asset | `Wave_Stage1` | `Wave_Stage2` |
| Reactive-teleport cooldown | n/a | 4 s |
| Shield HP threshold | 80% | always available |
| Phase-shift HP threshold | — | 50% |

## Out of Scope

- No AI script written here — separate planning pass.
- No new animator states, prefabs, or ScriptableObjects.
- No re-tuning of existing action durations (fade, thrust, charge).
- No third phase, no intro / outro cinematic, no music swap. If those are
  added later they'll layer on top of this brief, not replace it.
