# 03 The Wrong Ship Map

Target direction: enter from the north, descend into the ship, then climb back
north to return to `Warmrocks`.

```text
Legend:
  ...  main traversal path
  ---  optional/backtracking branch
  v    one-way fall / downward commitment
  ^    upward escape after boss reward
  [n]  scene number used in the reference below

                            from Warmrocks
                                  |
                                  v
+------------+     +------------+     +------------+     +------------+
| [1] Dead   |.....| [2] Bite-  |.....| [3] Barrel |.....| [4] Quiet  |
| Captain's |     | foot Deck  |     | Belly      |     | Cabin      |
| Landing   |     +------------+     +------------+     +-----+------+
+------------+                                                |
                                                              |
                                                        +-----+------+
                                                        | [5] Thunder|
                                                        | plank Pass.|
                                                        +-----+------+
                                                              |
                                                              v
                                                        +-----+------+
                                                        | [6] Bad    |
                                                        | Floor      |
                                                        +-----+------+
                                                              |
                                                              v
        +----------------------+                 +------------+-------+
        | [8] High Cargo Nest  |-----------------| [7] Turnbelly     |
        | optional later       |                 | Hold              |
        +----------------------+                 +------------+-------+
                                                              |
                                                              |
                                                        +-----+------+
                                                        | [9] Captain|
                                                        | Last Laugh |
                                                        +-----+------+
                                                              |
                                                              |
                                                        +-----+------+
                                                        | [10] Sigh- |
                                                        | ing Shaft  |
                                                        +-----+------+
                                                              ^
                                                              |
                                                       climb north to
                                                         Warmrocks
```

## Scene Reference

### 1. Ship Approach / Dead Captain's Landing

Traversal role:

- Entry from Warmrocks.
- Establishes the ship as combat territory.

POI spec:

- Ship entrance / deck access.
- Sabre pickup if not already acquired.
- Short warning note about the captain or the wreck.
- First clear return boundary back to hub.

Challenges and hazards:

- No required enemy if sabre is acquired here.
- Light platforming or broken plank step.

Items and rewards:

- Sabre if needed.
- A few coins or throwable sabres if sabre is already owned.

### 2. Deck Skirmish / Bitefoot Deck

Traversal role:

- First real melee scene.

POI spec:

- Flat combat lane.
- One walking shark placed with enough spacing for safe approach.
- Small healing drop or coin reward after the fight.

Challenges and hazards:

- Walking shark.
- Basic melee spacing.

Items and rewards:

- Coins or small healing bottle.

### 3. Barrel Hold / Barrel Belly

Traversal role:

- Teaches barrels before they are used under cannon pressure.

POI spec:

- Movable barrel.
- Breakable barrels.
- Simple blocked route or raised step solved with a barrel.
- Optional barrel hiding coins.

Challenges and hazards:

- Object pushing and breaking.
- No heavy combat.

Items and rewards:

- Coins.
- Healing bottle or throwable sabres from breakable barrels.

### 4. Pearl Cabin / Quiet Cabin

Traversal role:

- First stationary projectile enemy scene.

POI spec:

- Shell enemy in a readable room.
- Ricochet-friendly walls.
- Safe pockets where the player can wait.
- Chest beyond the projectile pattern.

Challenges and hazards:

- Shell / clam.
- Ricocheting pearls.

Items and rewards:

- Chest with coins, throwable sabres, or healing.

### 5. Cannon Corridor / Thunderplank Passage

Traversal role:

- Teaches timing and cover before the lower hold.

POI spec:

- Cannon line.
- Movable barrel cover.
- Breakable barrel reward on a safe side.
- Door or passage exit after the timing lane.

Challenges and hazards:

- Cannon fire.
- Explosion danger.
- Timing movement between cover points.

Items and rewards:

- Route forward.
- Optional coins or supply barrel.

### 6. Bilge Collapse / Bad Floor

Traversal role:

- One-way fall into the lower ship loop.

POI spec:

- Sea star encounter before the collapse.
- Cracked floor or weak plank visual.
- Clear one-way drop into lower hold.
- Safe landing area after the fall.

Challenges and hazards:

- Sea star charge.
- One-way commitment.

Items and rewards:

- Lower hold entry.

### 7. Cargo Mechanism Loop / Turnbelly Hold

Traversal role:

- Main lower hold objective loop.
- Opens the route to the first boss.

POI spec:

- Two steering wheel POIs.
- Locked boss route door.
- Mixed combat pockets separated by platforms or doors.
- One shark space, one shell/cannon space, and one safer objective pocket.

Challenges and hazards:

- Walking shark.
- Shell.
- Cannon line.
- Route reading under pressure.

Items and rewards:

- Boss route opens after both wheels.
- Optional coins or healing chest.

### 8. Upper Cargo Hold / High Cargo Nest

Traversal role:

- Optional return branch after traversal upgrade.

POI spec:

- Elevated cargo path visible earlier.
- High traversal route above the lower hold.
- Chest at the end of the branch.
- Shortcut back toward the boss route if useful.

Challenges and hazards:

- Elevated traversal.
- Cannon plus shell pressure.

Items and rewards:

- 1 diamond.
- Healing bottle, rum, or throwable sabres.

### 9. Captain's Cabin and Arena / Captain's Last Laugh

Traversal role:

- First boss sequence.

POI spec:

- Quiet preparation area before the fight trigger.
- Campfire.
- Supply chest near, but not inside, the arena.
- Vengeful Spirit arena.
- Reward pickup after victory.

Challenges and hazards:

- Vengeful Spirit boss.
- Teleporting, astral sabres, chase or melee punish windows.

Items and rewards:

- Grappling Hook.
- Rest and supplies before the fight.

### 10. Hook Escape and Teleporter / Sighing Shaft

Traversal role:

- Escape route after the boss.
- Returns north/up to Warmrocks.

POI spec:

- Simple vertical shaft.
- Easy anchor chain.
- Ship teleporter activation at the top or exit.
- Clear visual relief after the trapped lower hold.

Challenges and hazards:

- First required grappling hook use.
- No serious combat.

Items and rewards:

- Ship teleporter.
- Fast return to Warmrocks.

## Layout Notes

- The ship should physically descend after `Bad Floor`.
- `Sighing Shaft` should climb back toward Warmrocks, matching the global map.
- `High Cargo Nest` should remain optional and not block boss access.
