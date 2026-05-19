# Walkthrough Plan Lite

This is a lighter alternative to `docs/walkthrough-plan.md`. It keeps the same
core game arc, but reduces the number of scenes per location by roughly 30% by
combining adjacent tutorial, shop, secret, and shortcut POIs into fewer scenes.

Terminology:

- Location: a logically connected set of scenes in one biome or place.
- Scene: a Unity scene and the main planning unit inside a location.
- Point of interest (POI): a smaller meaningful spot inside a scene, such as a
  key item, tutorial setup, enemy encounter, puzzle, secret, campfire, or reward.

Player-facing map names are listed beside development scene names. English and
Russian names are adapted from `docs/scene-names.md`.

## Lite Scene Counts

| Development Location | Map Location EN | Map Location RU | Full Plan Scenes | Lite Scenes | Reduction |
|---|---|---|---:|---:|---:|
| Intro Beach / Intro Jungle Route | Driftwood Shore | Коряжий Берег | 8 | 7 | 13% |
| Island Hub / Rikko Camp | Warmrocks | Тёплые Камни | 9 | 6 | 33% |
| Wrecked Pirate Ship | The Wrong Ship | Не Тот Корабль | 14 | 10 | 29% |
| Hook Ruins / Upper Jungle / Cliffs | Cloud Mountain | Облачная Гора | 13 | 9 | 31% |
| Golden Skull Sanctuary | Skullkeeper Hollow | Лощина Сторожа Черепа | 12 | 8 | 33% |

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
9. Golden Skull Sanctuary opened
10. Stone Golem boss
11. Golden Skull chamber
12. Return to Rikko and escape

The lite structure should still feel connected, but it should require fewer
separate Unity scenes. Scenes can contain several POIs as long as the player can
read the objective clearly.

## Progression Gates

| Gate | Opens With | Main or Optional | Design Purpose |
|---|---|---|---|
| Vine-covered POI | Sabre | Optional | Teases combat utility before the player has a weapon. |
| High anchor POIs | Grappling Hook | Main and optional | Makes the hook a major traversal upgrade. |
| Candle POIs | Fireflies | Main and optional | Makes fireflies a remote activation tool, not only light. |
| Ship lower hold exit | Grappling Hook | Main | Forces immediate use of the boss reward. |
| Ruins mechanisms | Two steering wheels | Main | Creates a compact objective loop inside one location. |
| Sanctuary entrance | Ruins mechanism | Main | Marks late-game progression. |

## Main Walkthrough

### 1. Intro Beach / Intro Jungle Route

Map location name:

- EN: Driftwood Shore.
- RU: Коряжий Берег.

Player state:

- HP 5.
- No sabre.
- No grappling hook.
- No fireflies.

Purpose:

- Teach movement, jumping, spikes, doors, coins, and visible future secrets.
- Keep the first location short and readable.

Scenes:

| Development Scene | Map Name EN | Map Name RU | Access | Primary POIs / Challenges | Teaches | Reward or Result |
|---|---|---|---|---|---|---|
| Wake-up Shore | Drift-In Sand | Приблудный Песок | Start | Flat movement, coin line, small jump steps. | Movement, jumping, collection. | Coins. |
| Spike Shallows | Toothy Flats | Зубастые Плиты | Main path | First spike pit with safe retry space. | Spikes are readable hazards. | Progress. |
| Old Door Path | Shut-Stone Path | Тропа Запертого Камня | Main path | Simple switch or permanent door. | Doors control route progression. | Route opens. |
| Vine-Covered Alcove | Greenknife Nook | Угол Зелёных Ножей | Sabre later | Optional blocker beside the main route, covered with sharp greenknife vines. | Sabre opens some obstacles. | Coins, rum, or small chest. |
| Secret Overlook | Moon-Eye Overlook | Лунный Глаз | Hook and fireflies later | High anchor ledge plus candle cave POI. | Later abilities reveal old secrets. | 1-2 diamonds or rare item. |
| Secret Nook | Cloud Nook | Облачный Угол | Hook later | High ledge with a hook entry and reward pocket. | Hook opens old high places. | 1-2 diamonds or rare item. |
| Jungle Exit | Almost-Camp Trail | Тропа Почти-Там | Main path | Short combined platforming sequence. | Review movement and hazards. | Door to hub. |

Notes:

- Greenknife vines should look sharper and more dangerous than ordinary jungle
  plants, so the player understands this is a special blocker.
- `Secret Overlook` combines the full plan's hook ledge and candle cave into one
  compact optional scene.
- `Cloud Nook` is a separate hook-entry secret for a small high reward pocket.
- The main route should not require returning here.

### 2. Island Hub / Rikko Camp

Map location name:

- EN: Warmrocks.
- RU: Тёплые Камни.

Player state:

- HP 5.
- Main goal not yet understood.
- Sabre may be acquired here or at the ship entrance.

Purpose:

- Establish the central return point, Rikko, upgrades, supplies, and visible
  blockers.

Scenes:

| Development Scene | Map Name EN | Map Name RU | Access | Primary POIs / Challenges | Teaches | Reward or Result |
|---|---|---|---|---|---|---|
| Rikko Camp | Rikko's Porch | Крыльцо Рикко | Main path | Rikko dialog, Golden Skull objective, ending return. | Main quest and hub role. | Quest starts. |
| Campfire and Shop | The Warm Deal | Тёплая Сделка | Main path | Campfire, teleporter, Rikko shop. | Resting, recovery, supplies. | Checkpoint and shop access. |
| Training and Upgrade Yard | Hardhead Yard | Твердолобый Двор | Main path after sabre / diamonds | Training dummy and upgrade statue. | Sabre feedback and diamond upgrades. | Practice and upgrades. |
| Upper Anchor Path | Cloud Steps | Облачные Ступени | Hook later | Hook-gated route above camp. | Hub changes after hook. | Route to ruins. |
| Candle Gate | Little-Star Mouth | Пасть Малых Звёзд | Fireflies later | Firefly candle door or shortcut POI. | Fireflies open special paths. | Shortcut or reward. |
| Sanctuary Approach | Skullwatch Stones | Камни Черепьего Дозора | Ruins mechanism / optional early challenge | Sealed sanctuary door plus dangerous side path POI. | Final gate and optional risk. | Sanctuary access later, optional rewards. |

Notes:

- Rikko should point the player toward the ship.
- The hub can contain many POIs without splitting them into many scenes.

### 3. Wrecked Pirate Ship

Map location name:

- EN: The Wrong Ship.
- RU: Не Тот Корабль.

Player state:

- HP 5.
- Sabre damage 1.
- No grappling hook.
- No fireflies.

Purpose:

- Teach combat in layers and end with the first boss.
- Trap the player in the lower hold until the hook reward opens the escape.

Scenes:

| Development Scene | Map Name EN | Map Name RU | Access | Primary POIs / Challenges | Teaches | Reward or Result |
|---|---|---|---|---|---|---|
| Ship Approach | Dead Captain's Landing | Пристань Мёртвого Капитана | Main path | Entry, sabre pickup or sabre confirmation. | The ship is combat territory. | Sabre if needed. |
| Deck Skirmish | Bitefoot Deck | Кусающая Палуба | Main path | Walking shark on readable flat ground. | Basic melee spacing. | Coins or healing. |
| Barrel Hold | Barrel Belly | Бочечное Брюхо | Main path | Movable and breakable barrel POIs. | Barrels can block, reveal, or support routes. | Coins, route forward. |
| Pearl Cabin | Quiet Cabin | Тихая Каюта | Main path | Shell with ricocheting pearls. | Projectile reading and patience. | Chest or supplies. |
| Cannon Corridor | Thunderplank Passage | Проход Громовых Досок | Main path | Cannon fire with barrel cover. | Timing and cover under pressure. | Route forward. |
| Bilge Collapse | Bad Floor | Плохой Пол | Main path | Sea star charge and one-way fall into lower hold. | Fast enemy timing and point of no normal return. | Lower hold entry. |
| Cargo Mechanism Loop | Turnbelly Hold | Вертящее Брюхо | Main path | Mixed shark, shell, cannon, and two steering wheels. | Combined combat and mechanism objectives. | Boss route opens. |
| Upper Cargo Hold | High Cargo Nest | Высокое Грузовое Гнездо | Hook later optional | Elevated hook route above cargo hold. | Remember hook gates. | Diamond, parrot option, supplies. |
| Captain's Cabin and Arena | Captain's Last Laugh | Последний Смех Капитана | Main path | Campfire, supplies, Vengeful Spirit boss. | Boss preparation and first boss rhythm. | Grappling Hook. |
| Hook Escape and Teleporter | Sighing Shaft | Вздыхающая Шахта | Main path after boss | Anchor climb out of hold and ship teleporter. | First required hook use. | Fast return to hub. |

Notes:

- `Bilge Collapse` combines the sea star lesson and the one-way fall.
- `Cargo Mechanism Loop` compresses the two cargo loops but keeps two steering
  wheel POIs.
- `Captain's Cabin and Arena` can be one larger scene with a pre-boss safe POI.

### 4. Return to Hub and Backtracking Window

Player state:

- HP 5-6.
- Sabre damage 1.
- Grappling hook unlocked.
- Fireflies not yet unlocked.

Main steps:

1. Return to Rikko Camp through the ship teleporter.
2. Rikko acknowledges the hook and unlocks or sells fireflies.
3. The upper anchor path in the hub becomes reachable.
4. The player may optionally revisit intro or ship hook secrets.

Recommended optional visits:

| Optional Return | Requires | Reward |
|---|---|---|
| Intro Secret Overlook | Hook or fireflies | Diamond or rare item. |
| Ship Upper Cargo Hold | Hook | Diamond, parrot option, supplies. |
| Hub Dangerous Side Path | Skill or upgrades | Diamonds, rum, coins. |

### 5. Hook Ruins / Upper Jungle / Cliffs

Map location name:

- EN: Cloud Mountain.
- RU: Облачная Гора.

Player state:

- HP 5-6.
- Sabre damage 1.
- Grappling hook unlocked.
- Fireflies unlocked.

Purpose:

- Give the grappling hook a full location without spreading it across too many
  scenes.
- Teach fireflies first as visibility support, then as remote activators.

Scenes:

| Development Scene | Map Name EN | Map Name RU | Access | Primary POIs / Challenges | Teaches | Reward or Result |
|---|---|---|---|---|---|---|
| First Anchor Gap | Birdless Gap | Провал Без Птиц | Main path | Safe single-swing hook tutorial. | Hook attach, swing, release. | Progress. |
| Spike Swing Bridge | Needlewind Bridge | Мост Иголочного Ветра | Main path | Hook over spikes. | Hook timing now matters. | Progress. |
| Vertical Ruin Shaft | Ancestor's Throat | Горло Предков | Main path | Chained anchors upward. | Repeated hook use. | Upper route. |
| Totem Terrace | Meanstone Terrace | Злокаменная Терраса | Main path | Spike totem pressure while traversing. | Hook movement under ranged threat. | Chest or coins. |
| Split Mechanism Court | Two-Stone Court | Двор Двух Камней | Main path | Central hub with two wheel routes. | Explore branches to open the path. | Route logic starts. |
| Twin Wheel Branches | Forked Stone Ways | Раздвоенные Каменные Пути | Main path | West shell route and east floor-pressure route. | Hook under pressure and mechanism goals. | Steering wheels 1 and 2. |
| Hard Anchor Chain | Brave Fool's Way | Путь Храброго Дурака | Optional | Long hook sequence over spikes. | Hook mastery. | 1 diamond. |
| Dark Hook Cave | Blindglow Cave | Пещера Слепого Света | Fireflies | Dark entrance and hidden anchors. | Fireflies support visibility and hook traversal. | Progress. |
| Candle Mechanism Exit | Glowstone Door | Дверь Светящегося Камня | Main path | Candle switch, sanctuary mechanism, teleporter. | Fireflies activate unreachable objects. | Sanctuary opens, hub return. |

Notes:

- `Twin Wheel Branches` keeps two objectives inside one scene.
- `Candle Mechanism Exit` combines the firefly candle puzzle, sanctuary
  mechanism, and teleporter POI.

### 6. Golden Skull Sanctuary

Map location name:

- EN: Skullkeeper Hollow.
- RU: Лощина Сторожа Черепа.

Player state:

- HP 6-7 expected.
- Sabre damage 2 expected.
- Grappling hook required.
- Fireflies required.

Purpose:

- Final test of previous mechanics.
- Avoid adding major new required rules.

Scenes:

| Development Scene | Map Name EN | Map Name RU | Access | Primary POIs / Challenges | Teaches | Reward or Result |
|---|---|---|---|---|---|---|
| Sanctuary Entrance | Skullkeeper Door | Дверь Сторожа Черепа | Main path | Stone doors and light combat. | Final location has stricter pacing. | Progress. |
| Spike and Anchor Hall | Bonewind Hall | Зал Костяного Ветра | Main path | Hook swings over spikes with little margin. | Late-game hook confidence. | Progress. |
| Candle Hall | Star Nest Hall | Зал Звёздного Гнезда | Main path | Several candles in sequence. | Firefly puzzle at final complexity. | Main door opens. |
| Projectile Gallery | Stormwalk Gallery | Галерея Бродячей Бури | Main path | Shells and cannons in layered lanes. | Read projectile patterns under pressure. | Progress. |
| Totem Tower | Murmur Tower | Башня Шёпота | Main path | Totem shrine plus vertical hook tower. | Manage spawned threats during traversal. | Route to boss. |
| Treasure Annex | Don't-Tell Room | Комната Не-Скажу | Optional | Secret niche and dark firefly treasure POI. | Late-game optional mastery. | Diamonds and supplies. |
| Final Campfire and Golem Arena | Last Warm Floor | Последний Тёплый Пол | Main path | Final checkpoint and Stone Golem boss. | Prepare, dodge, and punish. | Golden Skull access. |
| Golden Skull Exit | Deep Hush | Глубокая Тишь | Main path | Golden Skull chamber and sanctuary teleporter. | Quest objective complete. | Return to Rikko. |

Notes:

- `Treasure Annex` combines the secret niche and dark treasure scene into one
  optional branch.
- `Final Campfire and Golem Arena` can be one scene with a clear safe POI before
  the fight trigger.

### 7. Return to Rikko and Ending

Player state:

- Golden Skull acquired.
- Stone Golem defeated.

Steps:

1. Use the sanctuary teleporter or connected route to return to Rikko Camp.
2. Talk to Rikko.
3. Rikko accepts the Golden Skull.
4. Ending sequence plays and the hero leaves the island.

Suggested Rikko line:

```text
A deal is a deal. Climb aboard before the island changes its mind.
```

## Optional Scenes and Backtracking

| Development Scene | Map Name EN | Map Name RU | First Seen | Opens With | Best Time to Return | Reward |
|---|---|---|---|---|---|---|
| Vine-Covered Alcove | Greenknife Nook | Угол Зелёных Ножей | Intro | Sabre | After weapon pickup. | Coins, rum, small chest. |
| Secret Overlook | Moon-Eye Overlook | Лунный Глаз | Intro | Hook or fireflies | After Vengeful Spirit / fireflies. | 1-2 diamonds or rare item. |
| Secret Nook | Cloud Nook | Облачный Угол | Intro | Grappling Hook | After Vengeful Spirit. | 1-2 diamonds or rare item. |
| Dangerous Side Path | Skullwatch Stones | Камни Черепьего Дозора | Hub | None, but difficult | After Vitality or Strength upgrade. | Diamonds, rum, coins. |
| Upper Cargo Hold | High Cargo Nest | Высокое Грузовое Гнездо | Ship | Grappling Hook | After boss or before ruins. | Diamond, parrot option, supplies. |
| Hard Anchor Chain | Brave Fool's Way | Путь Храброго Дурака | Ruins | Grappling Hook | During or after ruins. | 1 diamond. |
| Treasure Annex | Don't-Tell Room | Комната Не-Скажу | Sanctuary | Hook and fireflies | Before Stone Golem. | Diamonds, rare rum, large bottle. |

Backtracking should be encouraged through visible rewards, not forced through
unclear objectives. Teleporters should keep returns short after each major
location.

## Challenge Ramp

The player should learn mechanics in this order:

1. Move, jump, collect coins.
2. Avoid spikes in low-pressure layouts.
3. Read simple doors and visible blockers.
4. Learn sabre timing against a basic enemy.
5. Handle stationary projectiles.
6. Use barrels as cover and puzzle objects.
7. Handle faster enemies in constrained spaces.
8. Fight a readable first boss.
9. Use the grappling hook in a safe escape route.
10. Use hook swings over real hazards.
11. Chain hook anchors vertically.
12. Use fireflies for visibility.
13. Use fireflies to activate unreachable candles.
14. Combine hook traversal, fireflies, projectiles, and enemies.
15. Prepare for and defeat the final boss.

## Minimum Complete Route

If scope must be reduced further, keep this route:

1. Intro with visible future secrets.
2. Hub with Rikko, campfire, teleporter, shop, and sanctuary door.
3. Wrecked Ship with sabre, enemies, Vengeful Spirit, and grappling hook.
4. Hook Ruins with hook traversal and one firefly candle puzzle.
5. Sanctuary with combined traps, Stone Golem, and Golden Skull.
6. Return to Rikko and ending.

Do not cut:

- Sabre acquisition.
- Vengeful Spirit reward hook.
- Hook escape from ship.
- Fireflies if candle POIs remain on the main path.
- Sanctuary unlock.
- Stone Golem and Golden Skull reward.
