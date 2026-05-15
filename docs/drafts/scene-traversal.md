# Scene Traversal

This document summarizes the scenes from `docs/walkthrough-plan.md` as a
production inventory and traversal diagram.

Terminology follows `docs/walkthrough-plan.md`:

- Location: a logically connected set of scenes in one biome or place.
- Scene: a Unity scene and the main planning unit inside a location.
- Point of interest (POI): a smaller meaningful spot inside a scene, such as a
  key item, tutorial setup, enemy encounter, puzzle, secret, campfire, or reward.

## Scene Development List

The detailed challenge and reward breakdown for each location is in
`docs/walkthrough-plan.md`.

### Intro Beach / Intro Jungle Route

1. Wake-up Shore - start scene with flat movement and coin collection.
2. Broken Palm Steps - short jump and one-way platform tutorial.
3. Spike Shallows - first readable spike hazard with safe retry space.
4. Old Door Path - simple switch or permanent door progression.
5. Vine-Covered Alcove - optional sabre-gated POI with coins, rum, or a chest.
6. High Anchor Ledge - optional hook-gated POI with a diamond.
7. Candle Cave Mouth - optional firefly-gated POI with a candle puzzle reward.
8. Jungle Exit - final intro platforming scene leading to the hub.

### Island Hub / Rikko Camp

1. Rikko Camp - main NPC, Golden Skull objective, and later ending return.
2. Campfire Nook - checkpoint, rest, and recovery POI.
3. Training Corner - sabre practice POI after weapon acquisition.
4. Upgrade Statue - diamond upgrade POI.
5. Shop Shelf - Rikko shop POI for supplies and optional perks.
6. Upper Anchor Path - hook-gated scene leading toward the ruins.
7. Candle Gate - firefly-gated scene or shortcut.
8. Sanctuary Door - sealed final-location gate opened by ruins mechanisms.
9. Dangerous Side Path - optional difficult POI with diamonds and supplies.

### Wrecked Pirate Ship

1. Ship Approach - entry scene and sabre pickup or sabre confirmation.
2. Deck Skirmish - first walking shark melee encounter.
3. Barrel Hold - movable and breakable barrel puzzle scene.
4. Pearl Cabin - shell projectile-reading scene.
5. Cannon Corridor - cannon timing and cover scene.
6. Sea Star Bilge - charge-enemy scene in a constrained corridor.
7. Collapsing Deck - one-way fall into the lower hold.
8. Cargo Loop A - mixed shark and shell encounter, first steering wheel.
9. Cargo Loop B - cannon and barrel-cover encounter, second steering wheel.
10. Upper Cargo Hold - optional hook-gated POI with diamond and parrot option.
11. Captain's Cabin Campfire - pre-boss checkpoint and supply scene.
12. Vengeful Spirit Arena - first boss scene and grappling hook reward.
13. Hook Escape Shaft - required first hook traversal after the boss.
14. Ship Teleporter - fast-return scene back to the hub.

### Hook Ruins / Upper Jungle / Cliffs

1. First Anchor Gap - safe single-swing hook tutorial scene.
2. Spike Swing Bridge - hook traversal over real spike danger.
3. Vertical Ruin Shaft - chained anchor climb scene.
4. Totem Terrace - hook movement under ranged totem pressure.
5. Split Mechanism Court - central scene that branches to two wheel routes.
6. West Wheel Branch - shell plus hook traversal, steering wheel 1.
7. East Wheel Branch - floor-pressure encounter, steering wheel 2.
8. Hard Anchor Chain - optional hook mastery POI with a diamond.
9. Dark Cave Entrance - first firefly-readable dark scene.
10. Dark Hook Cave - fireflies reveal anchors for hook traversal.
11. Candle Switch Scene - fireflies light unreachable candles to open a door.
12. Ruins Exit Teleporter - fast-return scene to the hub.
13. Sanctuary Mechanism - final ruins objective that opens the sanctuary.

### Golden Skull Sanctuary

1. Sanctuary Entrance - final location entry with stone doors and light combat.
2. Spike and Anchor Hall - late-game hook precision scene.
3. Candle Hall - final firefly candle sequence.
4. Projectile Gallery - layered shell and cannon pressure scene.
5. Totem Shrine - spawned-threat management scene.
6. Hook Tower - vertical traversal with danger below.
7. Sanctuary Secret Niche - optional hook or firefly POI with a diamond.
8. Dark Treasure Scene - optional high-risk firefly treasure scene.
9. Final Campfire - final checkpoint and preparation scene.
10. Stone Golem Arena - final boss scene.
11. Golden Skull Chamber - quiet quest reward scene.
12. Sanctuary Teleporter - fast-return scene to Rikko Camp.

## Scene Traversal Diagram

Solid arrows show the main route. Dashed arrows show optional POI branches or
backtracking routes that can be taken after the required ability is unlocked.

```mermaid
flowchart TD
    Start([Start])
    End([Escape Ending])

    subgraph Intro["Intro Beach / Intro Jungle Route"]
        I1["Wake-up Shore"]
        I2["Broken Palm Steps"]
        I3["Spike Shallows"]
        I4["Old Door Path"]
        I5["Vine-Covered Alcove<br/>optional, sabre"]
        I6["High Anchor Ledge<br/>optional, hook"]
        I7["Candle Cave Mouth<br/>optional, fireflies"]
        I8["Jungle Exit"]
    end

    subgraph Hub["Island Hub / Rikko Camp"]
        H1["Rikko Camp"]
        H2["Campfire Nook"]
        H3["Training Corner<br/>after sabre"]
        H4["Upgrade Statue"]
        H5["Shop Shelf"]
        H6["Upper Anchor Path<br/>hook gate"]
        H7["Candle Gate<br/>firefly gate"]
        H8["Sanctuary Door<br/>ruins gate"]
        H9["Dangerous Side Path<br/>optional"]
    end

    subgraph Ship["Wrecked Pirate Ship"]
        S1["Ship Approach"]
        S2["Deck Skirmish"]
        S3["Barrel Hold"]
        S4["Pearl Cabin"]
        S5["Cannon Corridor"]
        S6["Sea Star Bilge"]
        S7["Collapsing Deck"]
        S8["Cargo Loop A<br/>wheel 1"]
        S9["Cargo Loop B<br/>wheel 2"]
        S10["Upper Cargo Hold<br/>optional, hook"]
        S11["Captain's Cabin Campfire"]
        S12["Vengeful Spirit Arena<br/>gain hook"]
        S13["Hook Escape Shaft"]
        S14["Ship Teleporter"]
    end

    subgraph Ruins["Hook Ruins / Upper Jungle / Cliffs"]
        R1["First Anchor Gap"]
        R2["Spike Swing Bridge"]
        R3["Vertical Ruin Shaft"]
        R4["Totem Terrace"]
        R5["Split Mechanism Court"]
        R6["West Wheel Branch<br/>wheel 1"]
        R7["East Wheel Branch<br/>wheel 2"]
        R8["Hard Anchor Chain<br/>optional"]
        R9["Dark Cave Entrance<br/>fireflies"]
        R10["Dark Hook Cave"]
        R11["Candle Switch Scene"]
        R12["Ruins Exit Teleporter"]
        R13["Sanctuary Mechanism"]
    end

    subgraph Sanctuary["Golden Skull Sanctuary"]
        G1["Sanctuary Entrance"]
        G2["Spike and Anchor Hall"]
        G3["Candle Hall"]
        G4["Projectile Gallery"]
        G5["Totem Shrine"]
        G6["Hook Tower"]
        G7["Sanctuary Secret Niche<br/>optional"]
        G8["Dark Treasure Scene<br/>optional, fireflies"]
        G9["Final Campfire"]
        G10["Stone Golem Arena"]
        G11["Golden Skull Chamber"]
        G12["Sanctuary Teleporter"]
    end

    Start --> I1 --> I2 --> I3 --> I4 --> I8 --> H1
    I4 -.->|return with sabre| I5 -.-> I4
    I8 -.->|return with hook| I6 -.-> I8
    I8 -.->|return with fireflies| I7 -.-> I8

    H1 --> H2 --> H5 --> S1
    H1 -.->|after sabre| H3 -.-> H1
    H1 -.->|diamonds| H4 -.-> H1
    H1 -.->|optional challenge| H9 -.-> H1

    S1 --> S2 --> S3 --> S4 --> S5 --> S6 --> S7
    S7 --> S8 --> S9 --> S11 --> S12 --> S13 --> S14 --> H1
    S9 -.->|return with hook| S10 -.-> S11

    H1 --> H6 --> R1
    H1 -.->|fireflies| H7 -.-> H1
    H1 -.->|post-hook backtracking| I6
    H1 -.->|post-hook backtracking| S10

    R1 --> R2 --> R3 --> R4 --> R5
    R5 --> R6 --> R5
    R5 --> R7 --> R5
    R5 -.->|optional hook mastery| R8 -.-> R5
    R5 --> R9 --> R10 --> R11 --> R13 --> R12 --> H1

    H1 --> H8 --> G1
    G1 --> G2 --> G3 --> G4 --> G5 --> G6 --> G9 --> G10 --> G11 --> G12 --> H1 --> End
    G6 -.->|optional secret| G7 -.-> G6
    G6 -.->|optional firefly treasure| G8 -.-> G9
```
