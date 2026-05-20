# Encounter Recommendations

Purpose: suggest small encounter corrections for the existing map docs so regular
enemies get enough reuse without making the compact game feel crowded.

These notes do not replace the map files. They identify the best places to add
or clarify enemy appearances if the current encounter plan feels too sparse.

## Design Goals

- Keep `Driftwood Shore` mostly safe and enemy-free.
- Keep `Warmrocks` readable as a hub, with danger only on optional side paths.
- Give each regular enemy at least one clear introduction and one later remix.
- Prefer recombining existing enemies, hazards, and traversal tools over adding
  new enemy types.
- Use optional branches for harder or more chaotic pairings.
- Avoid heavy combat inside puzzle rooms where firefly targeting or route
  reading is the main challenge.

## Highest Value Changes

| Priority | Location | Scene | Recommendation | Why |
| --- | --- | --- | --- | --- |
| 1 | The Wrong Ship | `Turnbelly Hold` | Add one required sea star lane after the `Bad Floor` intro. | Reinforces the sea star before it becomes optional or late-game content. |
| 2 | Warmrocks | `Skullwatch Stones` | Make the optional side path default to one sea star plus spikes. Add one weak spike totem only if the path has enough room. | Turns the hub side path into a useful enemy remix without affecting main hub safety. |
| 3 | Cloud Mountain | `Forked Stone Ways` | Add a second spike totem appearance on one wheel branch. | The spike totem currently risks feeling like a single-scene gimmick. |
| 4 | Cloud Mountain | `Blindglow Cave` | Add one low-pressure Big-Mouth Totem or loose fire skull preview. | Prepares the player for final-location spawned floor pressure. |
| 5 | Skullkeeper Hollow | `Murmur Tower` | Choose Big-Mouth Totem as the main-path totem, not an `or` option. | Makes the final tower's threat identity clear and reinforces fire skull behavior. |
| 6 | Skullkeeper Hollow | `Don't-Tell Room` | Choose Bird Totem as the optional mastery threat. | Keeps explosive flies dangerous and memorable without making the main route too noisy. |

## Enemy-Specific Notes

### Walking Shark

Current status: healthy enough for a compact game.

Recommended extra use:

- `Skullkeeper Hollow / Skullkeeper Door`: make the "one basic enemy" a walking
  shark if a simple combat reminder is needed.

Reasoning:

- The walking shark already teaches basic melee on the ship.
- A late, easy shark at the sanctuary entrance reminds the player of sabre
  spacing without introducing a new rule.
- It should be alone or paired only with a simple trap, since the final area
  should escalate after the entrance.

### Sea Star

Current risk: it has a clear introduction, but many later appearances are
optional or written as alternatives.

Recommended placements:

1. `The Wrong Ship / Turnbelly Hold`
   - Add one short corridor or cargo pocket where the sea star can charge and
     rebound.
   - Place it after the player has already seen the safer `Bad Floor` sea star.
   - Keep the pocket separated from shell and cannon pressure.

2. `Warmrocks / Skullwatch Stones`
   - Make the optional side path's default enemy one sea star.
   - Use spikes as the positional hazard.
   - Add a totem only if the path is clearly optional and has enough recovery
     space.

3. `Skullkeeper Hollow / Don't-Tell Room`
   - Use one sea star as a secondary optional threat only if Bird Totem pressure
     is not already intense.

Reasoning:

- The sea star is more interesting when it has room to accelerate, bounce, and
  force movement.
- It should not be stacked with too many projectile enemies, because its charge
  behavior needs readable space.

### Spike Totem

Current risk: it is introduced well in `Meanstone Terrace`, but needs one more
deliberate reuse.

Recommended placements:

1. `Cloud Mountain / Forked Stone Ways`
   - Put one spike totem on a wheel branch, firing across a hook release point
     or narrow landing.
   - Keep the branch short so the player can learn the rhythm before committing.

2. `Warmrocks / Skullwatch Stones`
   - Optional: add one weak or slow-firing spike totem beyond the sea star side
     path.
   - This should be a reward-side hazard, not a blocker on hub travel.

3. `Skullkeeper Hollow / Bonewind Hall`
   - Optional: use a single spike totem as a late hook precision modifier, but
     only if the spike pits alone are not enough.

Reasoning:

- Spike totems work best as platforming modifiers, not as room-clear enemies.
- Reusing one during a hook branch makes the Cloud Mountain combat language feel
  less isolated.

### Big-Mouth Totem and Fire Skulls

Current risk: if left only for the final tower, this enemy family may arrive too
late.

Recommended placements:

1. `Cloud Mountain / Blindglow Cave`
   - Add one low-pressure fire skull preview.
   - Best version: one Big-Mouth Totem placed behind terrain or in a small side
     pocket so the player sees where fire skulls come from.
   - Fallback version: one loose fire skull trap if a full totem is too much.

2. `Skullkeeper Hollow / Murmur Tower`
   - Make Big-Mouth Totem the default main-path tower threat.
   - Use fire skulls to control floor space while the player climbs.
   - Avoid mixing explosive flies into this same main-path scene unless testing
     shows the tower is too empty.

3. `Skullkeeper Hollow / Don't-Tell Room`
   - Do not use Big-Mouth Totem here if `Murmur Tower` already uses it heavily.
   - Let this optional branch feature Bird Totem instead.

Reasoning:

- Fire skulls combine naturally with jumping, hook traversal, and narrow floors.
- A preview in Cloud Mountain makes the final tower feel like escalation rather
  than a brand-new rule at the end.

### Bird Totem and Explosive Flies

Current risk: this is the most likely one-off enemy family.

Recommended placements:

1. `Cloud Mountain / Brave Fool's Way`
   - Optional first appearance.
   - Use one Bird Totem near the reward end of the mastery branch, after the
     hardest hook sequence.
   - Keep it avoidable or placed so the player can retreat.

2. `Skullkeeper Hollow / Don't-Tell Room`
   - Make Bird Totem the main optional treasure-room threat.
   - Pair it with darkness, candles, or spikes, but avoid adding cannons here.

3. `Skullkeeper Hollow / Murmur Tower`
   - Do not use Bird Totem on the main path if Big-Mouth Totem is selected
     there.

Reasoning:

- Explosive flies are reactive and chaotic, so they work best in optional
  mastery content.
- Two optional appearances are enough for a compact game if the enemy has strong
  visual and audio identity.

## Location Density Recommendations

### Driftwood Shore

Keep as-is. No mandatory enemies.

Allowed correction:

- `Greenknife Nook` can keep minor spikes or a tight platform step, but should
  not add an enemy unless the scene feels completely empty after sabre return.

### Warmrocks

Keep main hub scenes safe.

Best correction:

- `Skullwatch Stones`: make the optional side path a compact combat test:
  sea star, spikes, small reward, optional slow spike totem.

This gives the hub one dangerous edge without making repeated traversal
annoying.

### The Wrong Ship

Encounter density is already good.

Best correction:

- `Turnbelly Hold`: add a sea star pocket or lane.

Avoid adding more enemies to `Thunderplank Passage`, `Quiet Cabin`, or
`Captain's Last Laugh`; those scenes already have clear roles.

### Cloud Mountain

Combat density is intentionally lower because the hook is the main mechanic.

Best corrections:

- `Forked Stone Ways`: add one spike totem remix.
- `Blindglow Cave`: add one low-pressure fire skull or Big-Mouth Totem preview.
- `Brave Fool's Way`: optionally add one Bird Totem near the reward end.

These changes make Cloud Mountain feel less empty without competing with the
hook lessons.

### Skullkeeper Hollow

This should be the highest-density location, but the threats need clear
assignment.

Best corrections:

- `Skullkeeper Door`: use walking shark or a simple trap as the warm-up.
- `Stormwalk Gallery`: keep shells and cannons focused; no extra enemy needed.
- `Murmur Tower`: choose Big-Mouth Totem plus fire skulls.
- `Don't-Tell Room`: choose Bird Totem as the special optional threat.
- `Last Warm Floor`: keep boss preparation clean; no extra enemies before the
  trigger except supplies or harmless tension props.

## Suggested Final Enemy Coverage

| Enemy or hazard | Intro | Reinforcement | Late or optional remix |
| --- | --- | --- | --- |
| Walking shark | `Bitefoot Deck` | `Turnbelly Hold` | `Skullkeeper Door` optional reminder |
| Shell / clam | `Quiet Cabin` | `Turnbelly Hold` | `Stormwalk Gallery` |
| Cannon | `Thunderplank Passage` | `Turnbelly Hold` / `High Cargo Nest` | `Stormwalk Gallery` |
| Sea star | `Bad Floor` | `Turnbelly Hold` | `Skullwatch Stones` or `Don't-Tell Room` |
| Spike totem | `Meanstone Terrace` | `Forked Stone Ways` | `Skullwatch Stones` or `Bonewind Hall` |
| Big-Mouth Totem | `Blindglow Cave` preview | `Murmur Tower` | none needed |
| Fire skull | `Blindglow Cave` | `Murmur Tower` | optional light use only |
| Bird Totem | `Brave Fool's Way` optional | `Don't-Tell Room` optional | none needed |
| Explosive fly | `Brave Fool's Way` optional | `Don't-Tell Room` optional | none needed |
| Vengeful Spirit | `Captain's Last Laugh` | boss only | none |
| Stone Golem | `Last Warm Floor` | boss only | none |

## Minimal Patch Set

If only a few corrections are worth making, do these:

1. Add sea star reinforcement to `Turnbelly Hold`.
2. Make `Skullwatch Stones` side path default to sea star plus spikes.
3. Add a second spike totem to `Forked Stone Ways`.
4. Add Big-Mouth Totem or fire skull preview to `Blindglow Cave`.
5. Use Big-Mouth Totem in `Murmur Tower` and Bird Totem in `Don't-Tell Room`.

This keeps the game compact while making every non-boss enemy feel intentional.
