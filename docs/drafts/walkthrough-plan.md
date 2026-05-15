# Walkthrough Plan

This document breaks the compact pirate metroidvania structure from
`docs/general-game-info.md` into a suggested location-by-location walkthrough.

Terminology:

- Location: a logically connected set of scenes in one biome or place, such as
  the jungle, pirate ship, ruins, or sanctuary.
- Scene: a Unity scene and the main planning unit inside a location.
- Point of interest (POI): a smaller meaningful spot inside a scene, such as a
  key item, tutorial setup, enemy encounter, puzzle, secret, campfire, or reward.

## Walkthrough Overview

Main progression by location:

1. Intro Beach / Intro Jungle Route
2. Island Hub / Rikko Camp
3. Wrecked Pirate Ship
4. Vengeful Spirit boss
5. Grappling Hook escape from the ship
6. Return to Rikko Camp
7. Fireflies unlocked
8. Hook Ruins / Upper Jungle / Cliffs
9. Dark Firefly Scenes
10. Golden Skull Sanctuary opened
11. Stone Golem boss
12. Golden Skull chamber
13. Return to Rikko and escape

The structure should feel connected but controlled. The player sees several
future routes early, remembers them, and returns later after gaining the sabre,
grappling hook, or fireflies.

## Progression Gates

| Gate | Opens With | Main or Optional | Design Purpose |
|---|---|---|---|
| Vine-covered alcove | Sabre | Optional | Teases combat utility before the player has a weapon. |
| High anchor ledges | Grappling Hook | Main and optional | Makes the hook a major traversal upgrade. |
| Candle doors | Fireflies | Main and optional | Makes fireflies a remote activation tool, not only light. |
| Ship lower hold exit | Grappling Hook | Main | Forces the player to use the hook immediately after the boss. |
| Ruins stone mechanisms | Two steering wheels | Main | Creates a compact objective loop inside the ruins. |
| Sanctuary entrance | Ruins mechanisms | Main | Marks late-game progression. |
| Dangerous side paths | Player skill and upgrades | Optional | Gives early risk and later reward without blocking completion. |

Permanent progress should remain permanent: opened doors, lit candles, activated
teleporters, defeated bosses, obtained abilities, and collected unique rewards.

## Main Walkthrough

### 1. Intro Beach / Intro Jungle Route

Player state:

- HP 5.
- No sabre.
- No grappling hook.
- No fireflies.

Purpose:

- Teach movement, jumping, coins, spikes, simple doors, and readable secrets.
- Avoid combat pressure until the player reaches the hub and ship.

Scenes:

| Scene | Access | Primary POIs / Challenges | Teaches | Reward or Result |
|---|---|---|---|---|
| Wake-up Shore | Start | Flat movement with coins in a line. | Basic movement and collection. | Coins. |
| Broken Palm Steps | Main path | Small jumps and one-way platforms. | Jump height and landing control. | Coins. |
| Spike Shallows | Main path | Wide spike pit with safe retry space. | Spikes are dangerous but readable. | Progress. |
| Old Door Path | Main path | Simple permanent door or switch. | Doors can control route progression. | Route opens. |
| Vine-Covered Alcove | Sabre later | Visible blocked side path. | The sabre can open some blockers. | Coins, rum, or small chest. |
| High Anchor Ledge | Hook later | Anchor point above normal jump height. | Some treasures require the hook. | 1 diamond. |
| Candle Cave Mouth | Fireflies later | Unlit candles behind a low cave entrance. | Fireflies can unlock special scenes. | 1 diamond or rare item. |
| Jungle Exit | Main path | Slightly longer platform sequence. | Combines jumps, coins, and safe hazards. | Door to hub. |

Notes:

- The optional blockers should be visible but clearly out of reach.
- The main route should not require returning here.
- The first campfire can be placed near the exit if the intro is long enough.

### 2. Island Hub / Rikko Camp

Player state:

- HP 5.
- No weapon or newly acquired sabre, depending on where the sabre is placed.
- Main goal not yet understood.

Purpose:

- Establish Rikko, the Golden Skull objective, shop, campfire, teleporter,
  upgrade statue, and future route blockers.

Scenes:

| Scene | Access | Primary POIs / Challenges | Teaches | Reward or Result |
|---|---|---|---|---|
| Rikko Camp | Main path | Talk to Rikko. | The Golden Skull is needed to leave. | Main quest starts. |
| Campfire Nook | Main path | Safe interaction. | Resting, respawn, and recovery rhythm. | Checkpoint. |
| Training Corner | Main path after sabre | Training dummy with no danger. | Sabre timing and hit feedback. | Practice only. |
| Upgrade Statue | Main path | Read upgrade costs. | Diamonds are separate from coins. | Upgrade access. |
| Shop Shelf | Main path | Buy supplies when available. | Coins support preparation. | Bottles, sabres, rum, perks. |
| Upper Anchor Path | Hook later | Hook to high passage. | Hub route opens after hook. | Route to ruins. |
| Candle Gate | Fireflies later | Candle door near camp. | Fireflies unlock dark paths. | Dark scene access or shortcut. |
| Sanctuary Door | Ruins mechanisms | Large sealed stone door. | Final location is locked by world progress. | Sanctuary access later. |
| Dangerous Side Path | Early optional | Spikes and tougher enemy placement. | Optional risk can be deferred. | 1-2 diamonds, coins, rum. |

Notes:

- Rikko should point the player toward the ship.
- The sanctuary door should be visible early as a long-term objective.
- The hub teleporter should activate once the player reaches the camp.

### 3. Wrecked Pirate Ship

Player state:

- HP 5.
- Sabre damage 1.
- Throwable sabres may be available.
- No grappling hook.
- No fireflies.

Purpose:

- Teach combat in layers.
- Turn the ship into the first major combat dungeon.
- Trap the player in the lower hold until Vengeful Spirit is defeated and the
  grappling hook is obtained.

Scenes:

| Scene | Access | Primary POIs / Challenges | Teaches | Reward or Result |
|---|---|---|---|---|
| Ship Approach | Main path | Reach the deck and find or confirm sabre. | The ship is combat territory. | Sabre if not already acquired. |
| Deck Skirmish | Main path | One walking shark on flat ground. | Basic melee spacing. | Coins or healing. |
| Barrel Hold | Main path | Movable and breakable barrels. | Barrels can block, reveal, or support routes. | Coins, route forward. |
| Pearl Cabin | Main path | Shell shooting ricocheting pearls. | Projectile reading and patience. | Chest or supplies. |
| Cannon Corridor | Main path | Cannon fire with barrel cover. | Timing and cover under pressure. | Route forward. |
| Sea Star Bilge | Main path | Sea star charge in a limited corridor. | Bait, jump, and punish charge enemies. | Door switch. |
| Collapsing Deck | Main path | One-way fall into cargo hold. | This is a point of no normal return. | Player enters lower loop. |
| Cargo Loop A | Main path | Shark plus shell in separated spaces. | Combine melee and projectile threats. | First steering wheel. |
| Cargo Loop B | Main path | Cannon line and movable barrel cover. | Reuse barrel knowledge under danger. | Second steering wheel. |
| Upper Cargo Hold | Hook later optional | High anchor route above hold. | The player remembers hook gates. | Diamond, parrot cage/key option, supplies. |
| Captain's Cabin Campfire | Main path | Quiet preparation scene. | Boss is coming. | Rest, healing, throwable sabres. |
| Vengeful Spirit Arena | Main path | First boss fight. | Combat rhythm, dodging, punish windows. | Grappling Hook. |
| Hook Escape Shaft | Main path after boss | Simple anchor chain upward. | First required hook use. | Escape lower hold. |
| Ship Teleporter | Main path | Activate return point. | Teleporters reduce backtracking. | Fast return to hub. |

Notes:

- The lower hold fall should feel intentional, not like a soft lock.
- After the boss, the first hook climb should be easy and celebratory.
- The optional upper hold should be visible before the player can reach it.

### 4. Return to Hub and First Backtracking Window

Player state:

- HP 5-6.
- Sabre damage 1.
- Grappling hook unlocked.
- Fireflies not yet unlocked.

Purpose:

- Let the player feel the world has changed.
- Give Rikko a new shop or dialog beat.
- Allow a small amount of optional hook-based backtracking before the ruins.

Main steps:

1. Return to Rikko Camp through the ship teleporter or connected route.
2. Rikko acknowledges the hook and unlocks or sells fireflies.
3. The upper anchor path in the hub becomes reachable.
4. The player may backtrack to intro or ship secrets before continuing.

Recommended optional visits:

| Optional Return | Requires | Challenge | Reward |
|---|---|---|---|
| Intro High Anchor Ledge | Hook | One safe hook swing. | 1 diamond. |
| Ship Upper Cargo Hold | Hook | Cannon plus shell on elevated route. | 1 diamond, parrot option, supplies. |
| Hub Dangerous Side Path | Skill or upgrades | Sea star, spikes, maybe totem. | 1-2 diamonds, rum, coins. |

Notes:

- Fireflies should be guaranteed or cheap if they are required for main
  progression.
- Do not require heavy backtracking here. The player should be able to continue
  through the hub's upper hook path quickly.

### 5. Hook Ruins / Upper Jungle / Cliffs

Player state:

- HP 5-6.
- Sabre damage 1.
- Grappling hook unlocked.
- Fireflies unlocked.
- First diamond upgrade may be available.

Purpose:

- Give the grappling hook a full location.
- Teach fireflies first as visibility support, then as remote activators.
- Open the sanctuary through two compact objective branches.

Scenes:

| Scene | Access | Primary POIs / Challenges | Teaches | Reward or Result |
|---|---|---|---|---|
| First Anchor Gap | Main path | Single hook swing over a safe pit. | Hook attach, swing, release. | Progress. |
| Spike Swing Bridge | Main path | Hook over spikes. | Hook timing now matters. | Progress. |
| Vertical Ruin Shaft | Main path | Chain anchors upward. | Climbing with repeated hook use. | Upper route. |
| Totem Terrace | Main path | Spike totem covering platforms. | Hook movement under ranged pressure. | Chest or coins. |
| Split Mechanism Court | Main path | Two locked mechanism routes. | Player must explore both branches. | Opens route logic. |
| West Wheel Branch | Main path | Shell plus narrow hook gap. | Hook while managing projectiles. | Steering wheel 1. |
| East Wheel Branch | Main path | Sea star or fire skull floor pressure. | Keep moving while solving route. | Steering wheel 2. |
| Hard Anchor Chain | Optional | Long hook sequence over spikes. | Hook mastery. | 1 diamond. |
| Dark Cave Entrance | Fireflies | Low light and visible silhouettes. | Darkness is readable, not black. | Enters dark scene. |
| Dark Hook Cave | Main path | Fireflies reveal anchors. | Fireflies support traversal. | Progress. |
| Candle Switch Scene | Main path | Fireflies light candles behind bars or over spikes. | Fireflies activate unreachable objects. | Permanent stone door opens. |
| Ruins Exit Teleporter | Main path | Safe activation scene. | Ruins can be revisited later. | Hub return. |
| Sanctuary Mechanism | Main path | Final steering wheel or lightcatcher. | Ruins objective complete. | Sanctuary door opens. |

Notes:

- Avoid making dark scenes fully black.
- Lit candles should remain lit after death or rest.
- The hard anchor chain should not contain mandatory progression.

### 6. Golden Skull Sanctuary

Player state:

- HP 6-7 expected.
- Sabre damage 2 expected.
- Grappling hook required.
- Fireflies required.
- Mask, parrot, rum, and throwable sabres optional.

Purpose:

- Final test of previous mechanics.
- Combine hook traversal, fireflies, enemies, traps, and combat without adding a
  major new required rule.

Scenes:

| Scene | Access | Primary POIs / Challenges | Teaches | Reward or Result |
|---|---|---|---|---|
| Sanctuary Entrance | Main path | Stone doors and light combat. | Final location has stricter pacing. | Progress. |
| Spike and Anchor Hall | Main path | Hook swings over spikes with little margin. | Late-game hook confidence. | Progress. |
| Candle Hall | Main path | Fireflies light several candles in sequence. | Candle logic at final complexity. | Main door opens. |
| Projectile Gallery | Main path | Shells and cannons in layered lanes. | Read projectile patterns under pressure. | Progress. |
| Totem Shrine | Main path | Big-mouth or bird totem pressure. | Manage spawned threats. | Chest or key route. |
| Hook Tower | Main path | Vertical anchor chain plus enemies below. | Movement precision with danger nearby. | Route to campfire. |
| Sanctuary Secret Niche | Optional | Hook or firefly side route. | Spot and use a late-game secret. | 1 diamond. |
| Dark Treasure Scene | Optional fireflies | Bird totem, sea star, candles, spikes. | High-risk optional mastery. | 2 diamonds, large bottle, rare rum. |
| Final Campfire | Main path | Safe scene before boss. | Prepare for final fight. | Checkpoint, supplies nearby. |
| Stone Golem Arena | Main path | Laser, boomerang arm, ground slam. | Final combat test. | Golden Skull access. |
| Golden Skull Chamber | Main path | Quiet reward scene. | Quest objective complete. | Golden Skull. |
| Sanctuary Teleporter | Main path | Activate return. | Fast final return to hub. | Return to Rikko. |

Notes:

- The final sanctuary should test mastery, not surprise the player with new
  unreadable mechanics.
- Optional treasure should be useful before the boss but not mandatory.

### 7. Return to Rikko and Ending

Player state:

- Golden Skull acquired.
- Stone Golem defeated.

Steps:

1. Use sanctuary teleporter or connected route to return to Rikko Camp.
2. Talk to Rikko.
3. Rikko accepts the Golden Skull.
4. Ending sequence plays and the hero leaves the island.

Suggested Rikko line:

```text
A deal is a deal. Climb aboard before the island changes its mind.
```

## Optional Scenes and Backtracking

Optional scenes should be visible early and clearly gated, but they must not be
required for main completion.

| Scene | First Seen | Opens With | Best Time to Return | Reward |
|---|---|---|---|---|
| Intro Vine Alcove | Intro | Sabre | After reaching ship or hub weapon pickup. | Coins, rum, small chest. |
| Intro High Anchor Ledge | Intro | Grappling Hook | After Vengeful Spirit. | 1 diamond. |
| Intro Candle Cave | Intro | Fireflies | After Rikko unlocks fireflies. | 1 diamond or rare item. |
| Hub Dangerous Side Path | Hub | None, but difficult | After Vitality or Strength upgrade. | 1-2 diamonds, rum, coins. |
| Ship Upper Cargo Hold | Ship | Grappling Hook | After boss or before ruins. | 1 diamond, parrot option, supplies. |
| Hook Ruins Hard Anchor Chain | Ruins | Grappling Hook | During or after ruins. | 1 diamond. |
| Hook Ruins Candle Side Scene | Ruins | Fireflies | During ruins. | 1 diamond. |
| Sanctuary Secret Niche | Sanctuary | Hook or fireflies | Before Stone Golem. | 1 diamond or large bottle. |
| Sanctuary Dark Treasure Scene | Sanctuary | Fireflies | Before Stone Golem. | 2 diamonds, rare rum, large bottle. |

Backtracking should be encouraged through visible rewards, not forced through
unclear objectives. Teleporters should keep returns short after each major location.

## Challenge Ramp

The player should learn mechanics in this order:

1. Move, jump, collect coins.
2. Avoid spikes in low-pressure layouts.
3. Read simple doors and obvious blockers.
4. See optional gates before having the ability to open them.
5. Learn sabre timing against one basic walking enemy.
6. Handle stationary projectiles.
7. Use barrels as cover, blockers, and small puzzle objects.
8. Handle faster enemies in constrained spaces.
9. Fight a readable first boss with basic combat tools.
10. Use the grappling hook in a safe escape route.
11. Use hook swings over real hazards.
12. Chain multiple hook anchors vertically.
13. Use fireflies to improve dark scene visibility.
14. Use fireflies to activate candles beyond player reach.
15. Combine hook traversal, fireflies, projectiles, and enemies.
16. Prepare for and defeat the final boss.

Each new challenge should appear first in a simple form, then return later in a
combined form. For example, shells appear alone before shells appear with hook
movement or cannons.

## Possible Additional Enemy Types

The existing enemy set is enough for the compact version. If more variety is
needed, add only a few focused enemies with clear teaching roles.

| Enemy | Suggested Location | Behavior | Design Role |
|---|---|---|---|
| Hermit Crab Guard | Ship or sanctuary | Blocks attacks from the front, vulnerable from behind or after an attack. | Teaches positioning and patience. |
| Lantern Bat | Dark scenes | Slow flying patrol that glows faintly in darkness. | Gives vertical pressure without making darkness unfair. |
| Ruins Roller | Hook ruins or sanctuary | Rolls along a fixed path, can be jumped over or avoided with hook. | Adds timing pressure to traversal. |
| Cursed Candle Wisp | Firefly scenes | Appears near unlit candles and retreats when candles are lit. | Encourages using fireflies quickly. |

These enemies should not be required for the minimum complete version. Prefer
reusing walking sharks, shells, cannons, sea stars, and totems before expanding
the roster.

## Diamond and Reward Placement

Essential upgrades require about 5 diamonds:

- Vitality I: 2 diamonds.
- Strength I: 3 diamonds.

The world should contain more diamonds than required so the player can miss
some secrets and still upgrade.

Suggested distribution:

| Reward Scene / POI | Diamonds | Main or Optional |
|---|---:|---|
| Intro High Anchor Ledge | 1 | Optional |
| Intro Candle Cave | 1 | Optional |
| Hub Dangerous Side Path | 1-2 | Optional |
| Ship Upper Cargo Hold | 1 | Optional |
| Secret Ship Cabin or Barrel Puzzle | 1 | Optional |
| Hook Ruins Hard Anchor Chain | 1 | Optional |
| Hook Ruins Candle Side Scene | 1 | Optional |
| Ruins Main Path Chest | 1 | Main |
| Sanctuary Secret Niche | 1 | Optional |
| Sanctuary Dark Treasure Scene | 2 | Optional |

Recommended total: about 10-12 diamonds.

Main-route rewards should be enough to make at least one upgrade likely before
the sanctuary. Optional rewards should make the final boss easier, not decide
whether the game can be completed.

## Minimum Complete Route

If scope must be reduced, keep this route:

1. Intro route with visible future secrets.
2. Hub with Rikko, campfire, teleporter, shop, and sanctuary door.
3. Wrecked Ship with sabre, basic enemies, Vengeful Spirit, and grappling hook.
4. Hook Ruins with a short hook path and one firefly candle puzzle.
5. Sanctuary with combined traps, Stone Golem, and Golden Skull.
6. Return to Rikko and ending.

Optional cuts:

- Parrot cage branch.
- Mask upgrade branch.
- Hub dangerous side path.
- Advanced dark treasure scene.
- Extra enemy types.

Do not cut:

- Sabre acquisition.
- Vengeful Spirit reward hook.
- Hook escape from ship.
- Fireflies if candle doors remain on the main path.
- Sanctuary unlock.
- Stone Golem and Golden Skull reward.
