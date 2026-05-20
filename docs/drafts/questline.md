# Player Questline Draft

Source docs reviewed:

- `docs/general-game-info.md`
- `docs/walkthrough-plan-lite.md`
- `docs/scene-traversal-lite.md`

## Consistency Notes

The three docs agree on the main arc:

```text
Driftwood Shore
-> Warmrocks
-> The Wrong Ship
-> Warmrocks
-> Cloud Mountain
-> Warmrocks
-> Skullkeeper Hollow
-> Warmrocks
-> Ending
```

Small inconsistencies or risky points to resolve during implementation:

- Sabre timing is flexible in the docs. The general plan says the player should get the sabre after learning about the Golden Skull, while the lite walkthrough allows the sabre either in the hub or at the ship entrance. Clean player-facing route: Rikko sends the player toward the ship, and the player gets the sabre at `Ship Approach` unless it is already implemented earlier.
- Fireflies are required for main progression, but they are also described as a shop item. To avoid currency blocking the main route, fireflies should be guaranteed after the Vengeful Spirit, either free from Rikko or sold for a symbolic price.
- Cloud Mountain order differs slightly. The general overview places the dark firefly cave before the two-wheel mechanism, while the lite walkthrough places the two wheel branches before the dark cave and candle mechanism. Clean route: the player activates both ruin wheels, then uses fireflies in the dark/candle exit to open the sanctuary.
- `Sanctuary Approach` is both an early optional hub challenge and the late main entrance. This is workable, but it should be readable as two purposes in one area: an optional side path available early, and a sealed sanctuary door that only opens after Cloud Mountain.
- `Secret Overlook` is described as hook and firefly content, but the optional table says hook or fireflies. Best interpretation: the scene contains two optional rewards; hook reaches the high ledge, fireflies solve the candle POI, and full clear needs both.
- Strength I and 6-7 HP are expected before the final boss. The main route must provide enough diamonds for at least Vitality I and Strength I, or the Stone Golem should not be balanced as if those upgrades are mandatory.
- The one-way ship fall is a strong progression moment, but it can feel punitive if the player enters under-supplied. The lower ship should provide a campfire and enough supplies before Vengeful Spirit.
- The Golden Skull is guarded by a golem that is not necessarily evil. This creates a mild story tension: the hero is taking a protected treasure to pay Rikko. This fits the pirate tone, but notes or Rikko dialogue should keep it playful rather than accidentally heroic-serious.

## Main Questline

1. Wake up on Driftwood Shore.
   Learn movement, jumping, coins, spikes, and simple doors while moving inland. Visible blocked paths hint that the island can be revisited later.

2. Reach Warmrocks and meet Rikko.
   Rikko explains the deal: bring him the Golden Skull, and he will help the player leave the island. The shop, campfire, teleporter, upgrade statue, and training area establish this as the main hub.

3. Go to The Wrong Ship.
   Rikko points the player toward the wreck because it is the first useful lead and the place where the player can get armed.

4. Find or confirm the sabre at the ship entrance.
   The player can now fight, break certain blockers, use the training dummy later, and clear sabre-gated optional vines.

5. Push through the wrecked ship.
   Fight walking sharks, shells, cannons, sea stars, and barrel puzzles. A floor collapse drops the player into the lower hold, making the ship feel like a committed combat expedition.

6. Activate the ship mechanisms and reach the captain.
   The player turns the ship wheels, opens the boss route, rests at the pre-boss campfire, and prepares for Vengeful Spirit.

7. Defeat Vengeful Spirit and take the Grappling Hook.
   This is the first major progression reward. The player immediately uses the hook to climb out of the hold and reach the ship teleporter.

8. Return to Warmrocks.
   Rikko reacts to the hook and gives or unlocks fireflies. The player now has the two tools needed for the upper ruins: hook traversal and remote light activation.

9. Enter Cloud Mountain through the upper anchor path.
   The visible anchors above Warmrocks now make sense. The player follows the hook route into the ruins.

10. Master the hook route through Cloud Mountain.
    Cross anchor gaps, spike swings, vertical shafts, and totem pressure. The location teaches that the hook is now a core traversal ability, not only a shortcut tool.

11. Activate the two ruin wheels.
    From the central mechanism court, the player clears the west and east branches. Both wheels are needed to move the sanctuary mechanism forward.

12. Use fireflies in the dark cave and candle exit.
    Fireflies reveal anchors, light candles, and activate the final mechanism that opens the sanctuary. A teleporter returns the player to Warmrocks.

13. Prepare at Warmrocks.
    Spend diamonds on Vitality I and Strength I if available. Buy healing bottles, throwable sabres, rum, mask, or parrot if desired. The player is now guided back to the previously sealed sanctuary door.

14. Enter Skullkeeper Hollow from Sanctuary Approach.
    The door that was visible earlier is now open. This confirms that the ruins mechanism changed the hub and that the Golden Skull is close.

15. Clear the sanctuary trials.
    Use all learned tools: hook over spikes, firefly candle puzzles, projectile reading, totem pressure, and late-game combat.

16. Defeat the Stone Golem.
    The golem guards the Golden Skull. Winning the fight opens access to the treasure chamber.

17. Take the Golden Skull and use the sanctuary teleporter.
    The main objective is complete. The teleporter keeps the final return short.

18. Return to Rikko in Warmrocks.
    Give Rikko the Golden Skull. Rikko honors the deal, the player boards the ship, and the escape ending plays.

## Optional Visits

1. After getting the sabre: return to Driftwood Shore, `Vine-Covered Alcove`.
   Cut or break the greenknife vines for coins, rum, or a small chest. This teaches that earlier blockers are worth remembering.

2. After getting the grappling hook: return to Driftwood Shore, `Secret Overlook`.
   Use the high anchor route to reach a ledge reward, usually a diamond or rare item. Return again with fireflies if the candle POI is separate.

3. After getting the grappling hook: revisit The Wrong Ship, `Upper Cargo Hold`.
   Use hook anchors above the cargo area to reach an optional chest, diamond, supplies, or the parrot/cage reward.

4. After getting fireflies: check Warmrocks, `Candle Gate`.
   Light the candle door for a shortcut or reward. This reinforces that fireflies are useful outside dark caves.

5. After the first upgrade or when confident: try Warmrocks, `Sanctuary Approach` side path.
   This optional challenge can be visible early but is safer after Vitality, Strength, mask, parrot, or better supplies. Rewards can include diamonds, rum, and coins.

6. During Cloud Mountain: clear `Hard Anchor Chain`.
   This is the optional hook mastery route. The reason to go there is simple: the player sees a harder anchor path and takes it for a diamond.

7. Before Stone Golem: enter Skullkeeper Hollow, `Treasure Annex`.
   Use hook and fireflies to clear the dark treasure branch. This is the final optional preparation stop, rewarding diamonds, rare rum, a large healing bottle, or coins.

## Player Guidance

- Rikko gives the main objective and points toward the next required location.
- Visible blockers teach future returns: vines mean sabre, anchors mean hook, candles mean fireflies, sealed stone doors mean mechanisms.
- Campfires mark safe progress and boss preparation.
- Teleporters activate after major locations and make returns to Warmrocks fast.
- Bottled notes should hint at mechanics without long tutorials.
- Rewards should be visible before they are reachable, so optional backtracking feels intentional instead of random.
