# Global Map

This is a rough global footprint map for the lite world plan. It shows only the
outer shape and relative position of each location, not internal rooms or scene
details.

Scale is approximate: larger locations have larger footprints because they
contain more scenes.

| Location | Map Name | Scenes | Shape Intent |
|---|---|---:|---|
| Intro Beach / Intro Jungle Route | Driftwood Shore | 6 | Small horizontal route into the hub. |
| Island Hub / Rikko Camp | Warmrocks | 6 | Compact central return point. |
| Wrecked Pirate Ship | The Wrong Ship | 10 | Largest early location, long and low. |
| Hook Ruins / Upper Jungle / Cliffs | Cloud Mountain | 9 | Tall upper route, larger than hub. |
| Golden Skull Sanctuary | Skullkeeper Hollow | 8 | Late eastern location, compact but large. |

## Direction Rules

Local location maps should follow these relative directions:

| From | To | Direction |
|---|---|---|
| Driftwood Shore | Warmrocks | Northeast / upward and right. |
| Warmrocks | The Wrong Ship | South / downward. |
| The Wrong Ship | Warmrocks | North / upward after the ship escape. |
| Warmrocks | Cloud Mountain | North / upward. |
| Cloud Mountain | Warmrocks | South / return after opening the sanctuary route. |
| Warmrocks | Skullkeeper Hollow | East / right. |
| Skullkeeper Hollow | Warmrocks | West / return after the Golden Skull. |

## ASCII Footprint

```text
                         N
                         ^
                         |

                      +----------------------+
                      |                      |
                      |    CLOUD MOUNTAIN    |
                      |      9 scenes        |
                      |                      |
                      |                  +---+
                      |                  |
                      +------+           |
                             |           |
                             |           |
                  +----------+-----+     |
                  |                |     |
                  |   WARMROCKS    +-----+----------------+
                  |   6 scenes     |                      |
                  |                |   SKULLKEEPER HOLLOW |
     +------------+-----+----------+     8 scenes         |
     |                  |                |                |
     | DRIFTWOOD SHORE  |                +----------------+
     | 6 scenes         |
     |                  |
     +---------+--------+
               |
               |
        +------+-----------------------------+
        |                                    |
        |           THE WRONG SHIP           |
        |             10 scenes              |
        |                                    |
        +------------------------------------+

                         |
                         v
                         S
```

## Layout Notes

- `Warmrocks` is central because the player repeatedly returns there for Rikko,
  the shop, upgrades, gates, and the ending.
- `Driftwood Shore` sits west of the hub as the intro route.
- `The Wrong Ship` is lower and wider than the other locations because it has
  the most scenes and should feel like the first major dungeon.
- `Cloud Mountain` sits above the hub because it is the high route opened after
  the ship reward.
- `Skullkeeper Hollow` sits east of the hub and near Cloud Mountain because it
  is opened by the ruin mechanisms, but entered from the hub-side sanctuary
  approach.
- The shapes are not room layouts. They are only production-scale footprints for
  understanding world composition.
