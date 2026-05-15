# 01 Driftwood Shore Map

Target direction: northeast to `Warmrocks`.

```text
Legend:
  ...  main traversal path
  ---  optional/backtracking branch
  ^    upward/northward movement toward the next location
  [n]  scene number used in the reference below

                                                       to Warmrocks
                                                            ^
                                                            |
                                                    +-------+--------+
                                                    | [6] Almost-    |
                                                    |     Camp Trail |
                                                    +-------+--------+
                                                            ^
                                                            |
                                             +--------------+---+
                                             | [3] Shut-Stone   |
                                             |     Path         |
                                             +------+-----------+
                                                    |
                         +----------------------+   |   +----------------+
                         | [5] Moon-Eye         |---+---| [4] Greenknife |
                         |     Overlook         |       |     Nook       |
                         +----------------------+       +----------------+
                                                    optional returns

+------------+     +----------------+
| [1] Drift- |.....| [2] Toothy     |
|     In Sand|     |     Flats      |
+------------+     +-------+--------+
                          |
                          +..............................^
```

## Scene Reference

### 1. Wake-up Shore / Drift-In Sand

Traversal role:

- Start scene.
- Leads east into `Toothy Flats`.

POI spec:

- Player spawn / washed-up starting point.
- Short coin line guiding movement.
- 2-3 small jump steps with no enemy pressure.
- Optional bottle note for basic movement or island tone.
- Safe flat ground for the player to test controls.

Challenges and hazards:

- Basic walking and jumping only.
- No enemies.
- No lethal hazard.

Items and rewards:

- Small coin trail.

### 2. Spike Shallows / Toothy Flats

Traversal role:

- Main route from start toward the first door scene.

POI spec:

- First readable spike pit.
- Safe approach ledge before the pit.
- Safe recovery ledge after the pit.
- Optional coins placed above or near the safe jump arc.

Challenges and hazards:

- Spikes with forgiving retry space.
- No enemies.

Items and rewards:

- Coins.
- Progress to `Shut-Stone Path`.

### 3. Old Door Path / Shut-Stone Path

Traversal role:

- Main route hub for this location.
- Connects forward to `Almost-Camp Trail`.
- Shows optional returns to `Greenknife Nook` and `Moon-Eye Overlook`.

POI spec:

- Simple stone door.
- Obvious switch, lever, or pressure object.
- Visible locked/blocked optional side route.
- Sightline to a future high secret or sealed cave.

Challenges and hazards:

- Simple door logic.
- Small platforming step before or after the door.
- No combat.

Items and rewards:

- Permanent route opening.
- Optional coins near the switch.

### 4. Vine-Covered Alcove / Greenknife Nook

Traversal role:

- Optional branch from `Shut-Stone Path`.
- Intended for return after sabre acquisition.

POI spec:

- Sharp greenknife vines blocking the alcove entrance.
- Small alcove behind the vines.
- One chest or reward pocket.
- Visual distinction between greenknife vines and normal jungle plants.

Challenges and hazards:

- Sabre-gated blocker.
- Optional minor spikes or tight platform step if more challenge is needed.
- No required enemy.

Items and rewards:

- Coins.
- Rum or small chest.

### 5. Secret Overlook / Moon-Eye Overlook

Traversal role:

- Optional return branch.
- Intended after grappling hook and/or fireflies.

POI spec:

- High ledge visible from the main path.
- Anchor or high traversal target.
- Small candle cave or stone door POI.
- Secret reward chamber.

Challenges and hazards:

- Ability-gated high route.
- Light/candle interaction if fireflies are used here.
- Optional spikes near the reward, but not on the main intro route.

Items and rewards:

- 1-2 diamonds or rare item.
- Optional bottle note hinting that old places change after new tools.

### 6. Jungle Exit / Almost-Camp Trail

Traversal role:

- Final intro scene.
- Climbs northeast out of Driftwood Shore into Warmrocks.

POI spec:

- Short combined platforming sequence.
- A final coin trail pointing toward the hub.
- Door, path marker, or screen transition to Warmrocks.
- Optional view of Warmrocks rocks/camp smoke in the distance.

Challenges and hazards:

- Basic jumps plus one safe hazard reminder.
- No combat pressure.

Items and rewards:

- Coins.
- Route to Warmrocks.

## Layout Notes

- The location rises toward Warmrocks because Warmrocks sits higher on the
  global map.
- Optional branches should be visible but not required on the first pass.
- `Greenknife Nook` should sell the idea of sharp special vines before it
  becomes a recurring blocker.
