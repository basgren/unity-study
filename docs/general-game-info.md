# Project Overview — Small Pirate Metroidvania (Studying Unity)

## 1. High-Level Concept

The project is a small 2D pixel-art platformer / light metroidvania with a cartoon pirate adventure tone.

The player controls a small pirate kid, formerly a clown, who is stranded on a strange island after a
shipwreck. The main goal is simple: escape the island. To do that, the player must find Rikko, a lizard
merchant who owns or controls access to a ship. Rikko agrees to help the hero leave the island, but only
in exchange for the Golden Skull — an ancient treasure hidden deep inside the island’s sanctuary.

The game is designed as a compact but complete adventure with:

- one central hub;
- several connected locations and scenes;
- light backtracking;
- unlockable traversal and utility abilities;
- two bosses;
- secrets and optional upgrades;
- one ending.

The tone should be adventurous, readable, slightly mysterious, and humorous. The game should not aim for
realism. It should feel like a handcrafted cartoon pirate adventure with danger, secrets, strange
creatures, and playful dark fantasy elements.

---

## 2. Core Game Goal

The player’s main objective is:

Find Rikko
  → learn about the Golden Skull
  → get the sabre
  → explore the wrecked ship
  → defeat Vengeful Spirit
  → obtain the grappling hook
  → return to the hub
  → obtain fireflies
  → explore hook-based ruins and dark scenes
  → open the sanctuary
  → defeat the Stone Golem 
  → take the Golden Skull
  → return to Rikko
  → escape the island.


The story is intentionally simple. The gameplay structure and sense of progression are more
important than complex narrative.

---

## 3. Main Characters

## Player Character

The hero is a small pirate kid / former clown. The character should feel agile, expressive,
and slightly comedic, but still capable of surviving a dangerous island.

The player starts without a weapon and gradually gains tools, combat options, and traversal
abilities.

## Rikko

Rikko is a lizard merchant located in the central hub of the island.

Rikko serves several roles:

* main NPC;
* merchant;
* guide;
* source of light humor;
* progression marker;
* final escape provider.

Rikko explains that the player needs the Golden Skull to leave the island.

Possible Rikko line:

```text
Want to leave the island? Sure. Bring me the Golden Skull, and I’ll pretend I never saw you on my ship.
```

Rikko can sell:

* healing bottles;
* extra sabres for throwing;
* rum;
* fireflies;
* mask;
* parrot;
* possibly hints.

Rikko should be in or near the central hub so the player naturally returns to buy supplies and continue progression.

---

# 4. World Structure

The game is structured around a central hub with several connected locations.

Terminology:

* Location: a logically connected set of scenes in one biome or place, such as the jungle, pirate ship, ruins, or sanctuary.
* Scene: a Unity scene and the main planning unit inside a location.
* Point of interest (POI): a smaller meaningful spot inside a scene, such as a key item, tutorial setup, enemy encounter, puzzle, secret, campfire, or reward.

Player-facing map location names:

| Development Location | Map Name EN | Map Name RU |
|---|---|---|
| Intro Beach / Jungle Route | Driftwood Shore | Коряжий Берег |
| Island Hub / Rikko Camp | Warmrocks | Тёплые Камни |
| Wrecked Pirate Ship | The Wrong Ship | Не Тот Корабль |
| Hook Ruins / Cliffs | Cloud Mountain | Облачная Гора |
| Golden Skull Sanctuary | Skullkeeper Hollow | Лощина Сторожа Черепа |

```text
Intro Beach / Jungle Route
   ↓
Island Hub / Rikko Camp
   ├── Wrecked Pirate Ship
   ├── Hook Ruins / Cliffs
   ├── Dark Firefly Scenes
   └── Golden Skull Sanctuary
```

The game should feel connected, but it does not need to be a large open metroidvania. It is a compact island
adventure with controlled backtracking.

---

# 5. Main Locations / Biomes

## 5.1 Intro Beach / Intro Jungle Route

Player-facing map name:

* EN: Driftwood Shore
* RU: Коряжий Берег

This is the existing introductory scene and the first location.

The player starts without a sabre. The goal is to teach basic movement and platforming before combat is introduced.

### Main Features

* basic movement;
* jumping;
* platforms;
* spikes;
* coin collection;
* simple permanent doors;
* safe platforming challenges;
* possible first campfire;
* visible secrets that require later abilities.

### Purpose

This scene teaches:

* movement;
* jumping;
* avoiding spikes;
* collecting coins;
* understanding doors and route progression.

### Backtracking Secrets

The intro scene should contain optional secrets that the player can return to later.

Examples:

```text
Secret 1: requires sabre
A vine-covered or breakable path hides coins, rum, or a small chest.

Secret 2: requires grappling hook
A high ledge with an anchor leads to a diamond.

Secret 3: requires fireflies
A small candle puzzle opens a hidden stone door with a diamond or rare item.
```

These secrets must be optional. The player should not need to return here for the main path.

---

## 5.2 Island Hub / Rikko Camp

Player-facing map name:

* EN: Warmrocks
* RU: Тёплые Камни

This is the central location of the game.

### Main Features

* Rikko merchant;
* campfire;
* main teleporter;
* upgrade statue;
* training dummy;
* closed stone doors;
* routes to other locations;
* visible anchors for future hook use;
* candle or firefly secrets;
* bottles with notes.

### Purpose

The hub gives the player:

* the main goal;
* access to the shop;
* access to upgrades;
* a safe return point;
* a sense of progression as new routes open.

### Important Objects

* Rikko shop;
* campfire;
* teleporter;
* ancient upgrade statue;
* stone doors;
* steering wheel switches;
* training dummy;
* secret chests.

### Hub Blockers

```text
High ledge / upper passage:
Requires grappling hook.

Candle door:
Requires fireflies.

Stone door to ruins:
Requires progression through ship / hook route.

Door to sanctuary:
Requires activating mechanisms in the ruins.
```

---

## 5.3 Wrecked Pirate Ship

Player-facing map name:

* EN: The Wrong Ship
* RU: Не Тот Корабль

This is the first major combat location.

It uses the wooden pirate ship tileset.

### Main Features

* sabre acquisition;
* ship deck;
* breakable and movable barrels;
* cannons;
* shells;
* ricocheting pearls;
* walking sharks;
* sea stars;
* steering wheels;
* ship doors;
* chests;
* big cargo hold;
* Vengeful Spirit boss;
* grappling hook reward.

### Narrative Role

The ship belonged to a pirate captain who once tried to reach the Golden Skull. His spirit still haunts the ship.

### Suggested Structure

```text
Ship Entrance / Deck
→ Player finds or uses sabre
→ Ship floor collapses
→ Player falls into the large cargo hold
→ Player explores lower ship scenes
→ Campfire before boss
→ Vengeful Spirit fight
→ Player obtains grappling hook
→ Player uses hook to climb back out of the hold
→ Player opens teleporter back to hub
```

### Key Design Moment

The player falls into the cargo hold and cannot return upward normally. After defeating the boss and obtaining the grappling hook, the player uses the hook to climb back up.

This creates a strong progression moment:

```text
Fell into the hold → survived the ship → defeated the spirit → obtained the hook → escaped upward.
```

---

## 5.4 Hook Ruins / Upper Jungle / Cliffs

Player-facing map name:

* EN: Cloud Mountain
* RU: Облачная Гора

This is the location built around the grappling hook.

It can use jungle tiles, stone doors, ruin props, anchors, spikes, candles, and totems. A new full biome is not required.

### Main Features

* anchor chains;
* swinging over spikes;
* vertical shafts;
* hook-based traversal;
* totems;
* fire skulls;
* explosive flies;
* dark scenes;
* candle puzzles;
* steering wheel mechanisms;
* diamonds;
* optional danger scenes.

### Purpose

This location gives the grappling hook a full gameplay role. It should not feel like a minor upgrade. The player should use the hook to cross gaps, climb vertical spaces, reach switches, access secrets, and open the path to the sanctuary.

### Suggested Structure

```text
First hook traversal scene
→ Ruin platforms with spikes
→ Dark hook cave with fireflies
→ Two-route mechanism scene
→ Activate two steering wheels
→ Open the Golden Skull Sanctuary
→ Unlock teleporter back to hub
```

### Main Blockers

```text
High platforms:
Require grappling hook.

Dark cave / candle door:
Requires fireflies.

Stone door:
Requires activating two steering wheels.

Optional danger scene:
Can be entered early but is easier after upgrades.
```

---

## 5.5 Dark Firefly Scenes

Because the current lighting system can only darken an entire scene globally, dark challenges should be built as separate scenes.

### Main Features

* lower global light;
* candles;
* fireflies;
* hidden anchors;
* spikes;
* fire skulls;
* explosive flies;
* candle doors;
* lightcatcher objects.

### Purpose

These scenes give the fireflies a special role and make them different from a torch.

A torch lights the area around the player.

Fireflies can move independently and activate things where the player cannot reach.

### Recommended Use

Do not make scenes completely black. The player should still see silhouettes, important shapes, and hazards. Fireflies should improve visibility, reveal routes, and activate candles, but the scene should not become unreadable without them.

---

## 5.6 Golden Skull Sanctuary

Player-facing map name:

* EN: Skullkeeper Hollow
* RU: Лощина Сторожа Черепа

This is the final location.

It can use stone doors, candles, dark lighting, ruin props, jungle/stone tiles, gold highlights, spikes, totems, and traps.

### Main Features

* final combinations of previous mechanics;
* stone doors;
* candles;
* fireflies;
* hook traversal;
* totems;
* spikes;
* shell enemies;
* cannons;
* final campfire;
* Stone Golem boss;
* Golden Skull reward.

### Suggested Structure

```text
Sanctuary Entrance
→ Final trap scenes
→ Candle hall
→ Optional treasure scene
→ Final campfire
→ Stone Golem boss
→ Golden Skull chamber
→ Teleporter back to hub
→ Return to Rikko
→ Ending
```

### Narrative Role

The sanctuary contains the Golden Skull. The Stone Golem is its ancient guardian.

The golem is not necessarily evil. It exists to prevent anyone from taking the Skull away from the island.

---

# 6. Player Abilities

## 6.1 Basic Movement

Available from the start.

Includes:

* walking;
* running if supported;
* jumping;
* falling;
* platforming;
* interacting with objects.

Used in:

* intro scene;
* all platforming sections;
* spike challenges;
* hook setup scenes.

---

## 6.2 Sabre

The sabre is the player’s main melee weapon.

The first sabre must be found and becomes the permanent main weapon. It cannot be thrown.

### Functions

* melee attack;
* defeating enemies;
* breaking certain objects;
* opening sabre-based blockers;
* interacting with training dummy;
* cutting vines or weak obstacles if implemented.

### Recommended Progression

The player should receive the sabre before or at the beginning of the Wrecked Pirate Ship location.

### Base Damage

```text
Sabre damage before upgrade: 1
Sabre damage after upgrade: 2
```

---

## 6.3 Throwable Sabres

After the player has the main sabre, additional sabres can be collected or purchased. These extra sabres are consumable throwing weapons.

### Functions

* ranged attack;
* hitting enemies from a distance;
* possibly hitting switches;
* safer boss damage;
* useful against flying or dangerous enemies.

### Damage

```text
Throwable sabre base damage: 1
Throwable sabre upgraded damage: 2
```

### Sources

* Rikko shop;
* chests;
* secret scenes;
* pre-boss supply chests.

---

## 6.4 Grappling Hook

The grappling hook is a major traversal ability.

It allows the player to attach to special anchor points while jumping, swing on a rope, and cross gaps or climb upward.

### Functions

* swinging across spikes;
* climbing out of the ship cargo hold;
* reaching high platforms;
* accessing secrets;
* opening new routes;
* completing the hook-based ruins location.

### Acquisition

The grappling hook should be obtained after defeating Vengeful Spirit on the Wrecked Pirate Ship.

### Why It Matters

The hook should not be just an optional movement toy. It should be required for:

* escaping the ship’s cargo hold;
* reaching the hook ruins;
* accessing certain hub routes;
* opening optional secrets in earlier locations;
* progressing toward the sanctuary.

---

## 6.5 Fireflies

Fireflies are a utility / exploration perk.

They should not feel like a simple torch. Their unique identity is that they move independently from the player and can reach places the player cannot.

### Core Concept

```text
A torch lights where the player stands.
A firefly carries light away from the player.
```

### Recommended Mechanics

The player has a firefly jar with a limited number of fireflies, for example 3.

When activated:

* one firefly is released;
* it flies slightly ahead of the player;
* it illuminates the area around itself;
* it can fly over spikes, through gaps, or behind bars;
* it is attracted to special objects;
* it can ignite candles;
* it can activate lightcatchers;
* it can reveal anchors in dark scenes;
* after some time or after completing an action, it returns to the jar.

Fireflies should not be permanently consumed.

### Firefly Targets

Priority examples:

```text
1. Unlit candles
2. Lightcatchers
3. Hidden or dark anchors
4. Nearby exploration path
5. Random hovering around the player
```

### Uses

* lighting dark scenes;
* igniting candles;
* opening candle doors;
* revealing hidden paths;
* showing anchors in dark hook scenes;
* activating objects beyond player reach.

### Acquisition

Fireflies can be sold or given by Rikko after the player obtains the grappling hook.

If fireflies are required for main progression, they should be cheap or guaranteed.

---

## 6.6 Mask

The mask is an optional defensive perk.

### Function

When activated, it gives the player temporary invulnerability.

### Recommended Parameters

```text
Duration: 4–5 seconds
Cooldown: long
```

### Purpose

* helps weaker players survive difficult scenes;
* helps in dangerous optional scenes;
* helps during boss fights;
* provides a useful shop item without being required.

### Acquisition

Can be sold by Rikko after the first boss or found in an optional secret scene.

---

## 6.7 Parrot

The parrot is an optional attack helper.

### Function

When activated, it flies out and attacks the nearest enemy three times, then goes on cooldown.

### Recommended Parameters

```text
Base parrot hits: 3
Base damage per hit: 1
Upgraded damage per hit: 2
Cooldown: long
```

### Possible Acquisition

Option 1:

```text
Buy from Rikko.
```

Option 2:

```text
Find parrot in a cage on the Wrecked Pirate Ship.
Buy or find the cage key.
Free the parrot.
```

The second option feels more adventurous, but buying it from Rikko is simpler.

---

## 6.8 Healing Bottles

Healing bottles restore health.

### Types

```text
Small healing bottle: restores 2 HP
Large healing bottle: restores 4 HP
Carried healing bottle: restores 3 HP when activated
```

### Sources

* Rikko shop;
* chests;
* pre-boss supply POIs;
* secrets.

---

## 6.9 Rum

Rum is an optional consumable.

### Recommended Function

Temporarily increases the player’s sabre damage.

```text
Duration: 10–15 seconds
Effect: +1 sabre damage
```

Example:

```text
Sabre 1 damage → 2 damage with rum
Sabre 2 damage → 3 damage with rum
```

If this makes bosses too easy, reduce its effect against bosses or make it increase attack speed instead of raw damage.

### Role

* useful before boss fights;
* sold by Rikko;
* found in secret chests;
* pirate-themed and humorous.

---

# 7. Items and Collectibles

## 7.1 Coins

Coins are the main shop currency.

Used to buy:

* healing bottles;
* throwable sabres;
* rum;
* mask;
* parrot;
* fireflies;
* possibly hints.

Coins are found in:

* intro scene;
* chests;
* breakable barrels;
* secret scenes;
* enemy drops if desired.

---

## 7.2 Diamonds

Diamonds are rare upgrade currency.

They should be used at an ancient statue, not at Rikko. This gives coins and diamonds separate roles.

### Recommended Upgrades

Minimal version:

```text
Vitality I: +1 max HP
Strength I: sabre damage increases from 1 to 2
```

Optional version:

```text
Sharp Throws: throwable sabres deal 2 damage
Parrot Upgrade: parrot damage increases
Mask Upgrade: mask lasts longer
```

### Recommended Costs

```text
Vitality I: 2 diamonds
Strength I: 3 diamonds
Sharp Throws: 2–3 diamonds
```

The main route should provide enough diamonds for the first two upgrades without requiring excessive secret hunting.

---

## 7.3 Golden Skull

The Golden Skull is the main quest item.

It is found after defeating the Stone Golem in the final sanctuary.

### Function

The player gives the Golden Skull to Rikko in exchange for escape from the island.

### Narrative Role

The Golden Skull is the treasure Rikko wants. It motivates the entire adventure.

---

## 7.4 Keys

Keys can be used for:

* simple locked doors;
* optional chests;
* parrot cage;
* shortcuts.

Keys should not overcomplicate the main route. They are best used sparingly.

---

## 7.5 Chests

Chests can contain:

* coins;
* diamonds;
* throwable sabres;
* healing bottles;
* rum;
* mask;
* parrot-related key;
* notes;
* optional rewards.

Some chests should be visible but unreachable until the player gains a new ability.

---

## 7.6 Bottles With Notes

Bottles with notes provide narrative, hints, and tutorials.

They should replace long dialogue and keep the game readable.

### Functions

* guide the player;
* explain mechanics in-world;
* add humor;
* hint at secrets;
* warn about bosses.

Example notes:

```text
If you see an anchor above a pit, it is probably not decoration.

The candles here do not like matches. They like fireflies.

The captain is still on board. Technically dead. Practically annoyed.

Resting at a campfire gives you a break. It gives the island one too.
```

---

# 8. Obstacles and Interactive Objects

## 8.1 Spikes

Spikes are a major platforming hazard.

### Recommended Behavior

Spikes should not deal normal HP damage. Instead, they immediately respawn the player at the last safe point or checkpoint.

### Purpose

* platforming danger;
* hook challenges;
* dark scene danger;
* optional treasure risk.

---

## 8.2 Stone Doors

Stone doors are permanent progression blockers.

They can be opened by:

* steering wheels;
* candle puzzles;
* boss defeat;
* scripted progression;
* lightcatcher activation.

Once opened, they should remain open permanently.

---

## 8.3 Steering Wheels

Steering wheels act as switches.

They can open:

* ship doors;
* stone doors;
* gates;
* shortcuts;
* sanctuary entrance mechanisms.

They are useful because they fit the pirate theme and can act as readable interactables.

---

## 8.4 Campfires

Campfires are save and respawn points inspired by Dark Souls.

### Functions

* save progress;
* set respawn point;
* restore health;
* respawn enemies;
* possibly restore some resources.

### Important Rule

Enemies respawn after resting, but permanent progress remains.

Permanent progress includes:

* opened doors;
* activated teleports;
* lit candles;
* defeated bosses;
* obtained abilities.

---

## 8.5 Teleporters

Teleporters allow fast return to the hub.

### Recommended Rule

Teleporters activate only after the player reaches them physically.

### Locations

* central hub;
* after Wrecked Pirate Ship;
* after Hook Ruins;
* after Golden Skull Sanctuary;
* possibly before final boss or after final boss.

Teleporters reduce backtracking frustration while preserving the feeling of a connected world.

---

## 8.6 Barrels

There are two types:

### Movable Barrels

Used for:

* simple physics puzzles;
* blocking projectiles;
* reaching platforms;
* pressing switches if needed.

### Breakable Barrels

Used for:

* coins;
* healing items;
* throwable sabres;
* small secrets;
* blocking sabre-required paths.

---

## 8.7 Candles

Candles are light-based interactables.

### Types

```text
Normal candle:
Provides permanent light once lit.

Candle switch:
Counts toward opening a door.

Progress candle:
Stays lit after death/rest and makes the scene easier on return.
```

Candles can be lit by fireflies.

### Important Rule

Lit candles should stay lit permanently.

This creates a strong feeling of progress.

---

## 8.8 Lightcatchers

Optional planned prop.

A lightcatcher is a special object that attracts fireflies.

### Possible Visuals

* glowing flower;
* crystal;
* ancient lamp;
* firefly idol;
* lantern mushroom.

### Function

When a firefly reaches it, it can:

* open a door;
* activate a mechanism;
* light nearby candles;
* reveal a secret;
* temporarily disable a hazard.

Lightcatchers help make fireflies feel different from a torch.

---

## 8.9 Anchors

Anchors are grappling hook points.

### Uses

* swinging over spikes;
* climbing upward;
* crossing gaps;
* reaching secrets;
* escaping the ship hold;
* navigating hook ruins.

Anchors should be visually clear. In dark scenes, fireflies can help reveal them.

---

## 8.10 Training Dummy

The training dummy is a humorous and functional prop.

### Uses

* teaching sabre attacks;
* allowing the player to test damage;
* showing hit feedback;
* adding character to the hub or ship.

It does not need to be mechanically deep.

---

# 9. Enemies

## 9.1 Walking Shark

A basic melee enemy that walks on land.

### Role

* early combat tutorial enemy;
* patrol threat;
* simple timing challenge.

### Recommended Stats

```text
HP: 3
Damage: 1
```

With sabre damage 1, it takes 3 hits.
With sabre damage 2, it takes 2 hits.

---

## 9.2 Shell / Clam

A stationary or semi-stationary enemy that spits pearls.

### Attack

* shoots pearls;
* pearls can ricochet from surfaces;
* can force the player to move, jump, or wait.

### Recommended Stats

```text
HP: 3
Pearl damage: 1
Ricochet count: up to 3
```

### Role

* teaches projectile avoidance;
* creates interesting scene geometry problems;
* works well with platforms and narrow ship scenes.

---

## 9.3 Cannon

A trap or enemy-like object that fires explosive cannonballs.

### Attack

* shoots straight cannonballs;
* cannonballs explode on impact;
* explosion deals higher damage.

### Recommended Stats

```text
Cannon HP: optional / can be invulnerable
Cannonball explosion damage: 2
```

### Role

* positional hazard;
* cover-based challenge;
* works with barrels and ship corridors.

Cannons may be indestructible if simpler.

---

## 9.4 Sea Star

A walking enemy that charges when it sees the player.

### Behavior

* patrols normally;
* detects player;
* spins up;
* accelerates toward the player;
* bounces off obstacles;
* returns to normal after some time.

### Recommended Stats

```text
HP early/mid: 5
HP late: 6
Damage while charging: 2
Normal contact damage: 1
```

### Role

* creates panic;
* punishes careless movement;
* works well in corridors and bounce arenas.

---

## 9.5 Spike Totem

A totem that shoots spikes or stakes.

### Attack

* fires single spikes or bursts;
* can be activated by proximity, trigger, or scene logic.

### Recommended Stats

```text
HP: 4–6
Spike damage: 1
```

### Role

* timing hazard;
* ranged pressure;
* platforming modifier.

---

## 9.6 Big-Mouth Totem

A totem that spawns fire skulls.

### Fire Skull Behavior

* moves low along the ground;
* turns around when hitting obstacles;
* has limited lifetime;
* damages on contact.

### Recommended Stats

```text
Totem HP: 5–6
Fire skull damage: 1
Fire skull lifetime: limited
```

### Role

* controls floor space;
* combines well with jumping, hook traversal, and narrow corridors.

---

## 9.7 Bird Totem

A totem that spawns explosive flies.

### Explosive Fly Behavior

* flies normally or waits;
* detects nearby player;
* starts chasing;
* explodes at close distance.

### Recommended Stats

```text
Bird totem HP: 4–5
Fly HP: 1–2
Fly explosion damage: 2
```

### Role

* pressure enemy;
* forces quick reactions;
* punishes standing still.

---

# 10. Bosses

## 10.1 Boss 1 — Vengeful Spirit

Vengeful Spirit is the first major boss.

### Location

Wrecked Pirate Ship.

### Narrative Role

The spirit of a dead pirate captain haunts the ship. He guards the captain’s treasure and the grappling hook.

### Gameplay Role

This boss acts as the gate to the grappling hook.

### Attacks

* teleportation;
* astral sabres;
* chasing the player;
* melee strike;
* possibly blink attack;
* disappearing and reappearing.

### Recommended Stats

```text
HP: 20
Common attack damage: 1
Strong attack damage: 2
Expected player HP: 5
Expected player sabre damage: 1
```

### Reward

```text
Grappling Hook
```

### Design Goal

The boss should test early combat skills but should not require upgrades. The player should be able to defeat it with the basic sabre and some healing.

---

## 10.2 Boss 2 — Stone Golem

Stone Golem is the final boss.

### Location

Golden Skull Sanctuary.

### Narrative Role

The ancient guardian of the Golden Skull. It prevents the treasure from leaving the island.

### Gameplay Role

Final test of combat, positioning, timing, and resource preparation.

### Attacks

1. Laser

```text
The golem aims, gives a clear warning, then fires a straight beam.
Damage: 2
```

2. Boomerang Arm

```text
The golem throws its arm forward.
The arm damages on the way out and on the way back.
Damage: 1 per hit, or 2 if tuned harder.
```

3. Ground Slam

```text
The golem hits the ground and sends a wave or stream of stones.
Damage: 2 for slam, 1 for smaller stones.
```

4. Phase 2 Pressure

```text
Below 50% HP, attacks become faster or combine with extra stones.
```

### Recommended Stats

```text
HP: 40
Laser damage: 2
Boomerang arm damage: 1–2
Ground slam damage: 2
Stone damage: 1
Expected player HP: 6–7
Expected sabre damage: 2
```

### Reward

```text
Golden Skull
```

### Design Goal

The boss should be more demanding than Vengeful Spirit, but still readable. The player should understand when to dodge and when to attack.

---

# 11. Player Progression and Balance

## 11.1 Health Progression

```text
Start: 5 HP
After Vitality upgrade: 6 HP
Optional maximum: 7 HP
```

The diploma version should not exceed 7 HP unless the game becomes too difficult.

---

## 11.2 Damage Progression

```text
Before sabre: no attack
Base sabre: 1 damage
Upgraded sabre: 2 damage
Base throwable sabre: 1 damage
Upgraded throwable sabre: 2 damage
Rum bonus: +1 temporary sabre damage
```

---

## 11.3 Expected Progression by Location

```text
Intro Beach / Jungle Route:
HP 5
No sabre
No hook
No fireflies

Hub:
HP 5
Main goal received
Shop unlocked

Wrecked Ship:
HP 5
Sabre 1 damage
Throwable sabres available
No hook yet

After Vengeful Spirit:
HP 5–6
Sabre 1 damage
Hook unlocked

Hook Ruins:
HP 5–6
Sabre 1 damage
Hook unlocked
Fireflies unlocked
First upgrade expected

Before Sanctuary:
HP 6
Sabre upgraded to 2 damage
Fireflies and hook required
Optional mask/parrot/rum

Before Stone Golem:
HP 6–7
Sabre 2 damage
Throwable sabres 1–2 damage
Healing bottles available
Optional mask/parrot/rum
```

---

# 12. Upgrade Plan

## Essential Upgrades

Only two upgrades are required for the compact version:

```text
Vitality I:
Cost: 2 diamonds
Effect: +1 max HP

Strength I:
Cost: 3 diamonds
Effect: sabre damage increases from 1 to 2
```

## Optional Upgrades

```text
Sharp Throws:
Cost: 2–3 diamonds
Effect: throwable sabres deal 2 damage

Better Parrot:
Cost: 2–3 diamonds
Effect: parrot deals more damage

Longer Mask:
Cost: 2–3 diamonds
Effect: mask lasts longer
```

---

# 13. Dangerous Optional Scenes

Some optional scenes can be accessible early but balanced to be difficult until the player returns later with upgrades.

These optional scenes should never contain mandatory progression items.

## Example 1 — Side Path Near Hub

Accessible early.

Contains:

* sea star;
* spikes;
* small chest;
* maybe a totem.

Early player:

```text
HP 5
Sabre 1
Difficult
```

Later player:

```text
HP 6–7
Sabre 2
Mask/parrot possible
Much easier
```

Reward:

* diamond;
* rum;
* coins.

---

## Example 2 — Upper Cargo Hold

Visible during first ship visit, but requires hook.

Contains:

* cannon;
* shell;
* chest;
* optional parrot cage.

Reward:

* diamond;
* parrot;
* healing bottle;
* coins.

---

## Example 3 — Dark Treasure Scene in Sanctuary

Accessible before final boss.

Contains:

* bird totem;
* sea star;
* candles;
* spikes;
* fireflies required.

Reward:

* 2 diamonds;
* rare rum;
* large healing bottle;
* coins.

---

# 14. Suggested Economy

## Shop Prices

```text
Small healing bottle: 10 coins
Large healing bottle: 25 coins
Extra sabre: 15 coins
Bundle of 3 sabres: 35 coins
Rum: 30 coins
Fireflies: 30–50 coins
Mask: 70 coins
Parrot: 80–120 coins
Parrot cage key: 50 coins
```

If fireflies are required for main progression, they should be cheap or guaranteed.

---

# 15. Diamond Distribution

The game should contain more diamonds than required for the essential upgrades.

Example distribution:

```text
Intro scene hook secret: 1
Intro scene firefly secret: 1
Upper ship hold: 1
Secret ship cabin: 1
Hook ruins hard anchor chain: 1
Hook ruins candle puzzle: 1
Dangerous side path: 2
Sanctuary secret niche: 1
Sanctuary treasure scene: 2
```

Total: around 11 diamonds.

Essential upgrades require 5 diamonds.

This allows players to miss some secrets and still progress.

---

# 16. Location-by-Location Progression Summary

## Location 0 — Intro Beach / Jungle Route

### Player State

```text
HP: 5
Sabre: no
Hook: no
Fireflies: no
```

### Main Goal

Reach the island hub.

### Challenges

* platforms;
* spikes;
* coins;
* simple doors.

### Optional Future Secrets

* sabre-blocked path;
* hook ledge;
* firefly candle door.

---

## Location 1 — Island Hub

### Player State

```text
HP: 5
Sabre: possibly no / soon
Goal: find Golden Skull
```

### Main Goal

Meet Rikko and learn the main objective.

### Key Features

* shop;
* campfire;
* teleporter;
* upgrade statue;
* training dummy;
* visible blockers.

### Opens Next

Wrecked Pirate Ship.

---

## Location 2 — Wrecked Pirate Ship

### Player State

```text
HP: 5
Sabre: yes
Sabre damage: 1
Hook: no
Fireflies: no
```

### Main Goal

Defeat Vengeful Spirit and obtain the grappling hook.

### Challenges

* walking sharks;
* shells;
* cannons;
* sea stars;
* barrels;
* ship doors;
* steering wheels;
* cargo hold;
* boss fight.

### Reward

```text
Grappling Hook
```

### Opens Next

* hook-based backtracking;
* hook ruins;
* upper secrets;
* escape from cargo hold.

---

## Location 3 — Hook Ruins / Dark Scenes

### Player State

```text
HP: 5–6
Sabre damage: 1
Hook: yes
Fireflies: yes
First upgrade expected
```

### Main Goal

Use the hook and fireflies to activate mechanisms and open the sanctuary.

### Challenges

* anchor chains;
* spikes;
* totems;
* fire skulls;
* explosive flies;
* dark scenes;
* candle puzzles;
* steering wheels.

### Reward

```text
Sanctuary entrance opened
Teleporter unlocked
Diamonds for upgrade
```

### Expected Upgrade After This

```text
Strength I:
Sabre damage becomes 2
```

---

## Location 4 — Golden Skull Sanctuary

### Player State

```text
HP: 6–7
Sabre damage: 2
Hook: yes
Fireflies: yes
Optional mask/parrot/rum
```

### Main Goal

Defeat the Stone Golem and obtain the Golden Skull.

### Challenges

* final trap combinations;
* candle hall;
* hook sections;
* totems;
* cannons;
* optional treasure scene;
* final boss.

### Reward

```text
Golden Skull
```

### Opens Final

Return to Rikko and escape.

---

# 17. Ending

The player returns to Rikko with the Golden Skull.

Rikko accepts the Skull as payment and allows the player to leave the island.

Possible final line:

```text
A deal is a deal. Climb aboard before the island changes its mind.
```

The hero leaves the island.

There is only one ending.

---

# 18. Design Priorities

## Main Priorities

1. The game must be readable.
2. Controls must feel responsive.
3. Every enemy and obstacle must have a clear gameplay role.
4. The hook must be a major traversal ability, not a minor gimmick.
5. Fireflies must feel different from a torch.
6. Rikko should make the hub feel alive.
7. Backtracking should reward the player without becoming annoying.
8. Teleporters should reduce travel friction.
9. Campfires should create rhythm and make the world feel alive through enemy respawn.
10. The game should feel complete even with a small scope.

---

# 19. What to Keep Mandatory

These are the core features that make the game feel complete:

```text
Intro scene
Island hub
Rikko
Campfires
Teleporters
Sabre
Throwable sabres
Wrecked ship
Vengeful Spirit
Grappling hook
Hook traversal location
Fireflies
Candle puzzle
Golden Skull Sanctuary
Stone Golem
Golden Skull
Final return to Rikko
Escape ending
```

---

# 20. What Can Be Optional

These features are useful but can be cut if time is limited:

```text
Parrot
Mask
Rum
Advanced diamond upgrades
Many secret scenes
Complex shop progression
Many note bottles
Lightcatcher objects
Multiple dangerous optional scenes
Damage numbers on training dummy
```

---

# 21. Minimal Complete Version

If the project needs to be reduced, the smallest complete version is:

```text
1. Intro scene without sabre.
2. Hub with Rikko, campfire, teleporter, and goal.
3. Wrecked Ship with sabre, enemies, Vengeful Spirit, and grappling hook.
4. Hook Ruins with hook traversal and one firefly candle puzzle.
5. Sanctuary with final traps, Stone Golem, and Golden Skull.
6. Return to Rikko and escape.
```

This is enough to feel like a full small game.

---

# 22. Final One-Paragraph Pitch

This game is a compact 2D pixel-art pirate metroidvania about a small pirate kid stranded on a strange island. To escape, the hero must bring the Golden Skull to Rikko, a lizard merchant with a ship. The player begins with simple platforming, then finds a sabre, explores a wrecked pirate ship, defeats the Vengeful Spirit, obtains a grappling hook, returns to earlier locations for secrets, uses fireflies to light candles and reveal dark paths, opens ancient ruins, defeats the Stone Golem guarding the Golden Skull, and finally returns to Rikko to leave the island. The game focuses on readable platforming, clear enemies, light backtracking, useful upgrades, humorous notes, and a complete adventure structure built from a small set of strong mechanics.

```
```
