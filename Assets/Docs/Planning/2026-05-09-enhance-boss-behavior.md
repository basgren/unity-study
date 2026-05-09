We need to refactor and implement the Vengeful Spirit boss AI in Unity/C# using a clear
three-layer behavior architecture.

Context:
The boss is a flying skeleton mage for a 2D pixel-art platformer. The fight should feel
readable, rhythmic, and not like random attack spam. The boss has several attacks,
phase-based behavior, teleportation, casting, charge attacks, and a spectral shield.

Existing components:
1. VengefulSpirit.cs
    - This component should represent the physical/action layer.
    - It should execute low-level boss abilities:
        - movement
        - teleport
        - attack animation triggers
        - damage windows
        - casting
        - sword spawning
        - shield spawning
        - charge movement
        - disappearing/reappearing
    - It should NOT decide fight strategy.
    - It should expose async/coroutine-friendly methods or commands that higher AI layers can call.

2. VengefulSpiritAI.cs
    - Currently contains tactical and strategic behavior.
    - Refactor if useful.
    - Preferred architecture:
        - VengefulSpiritAI keeps the strategic layer:
            - phase selection
            - choosing the next tactical pattern
            - enforcing phase rules
            - reacting to boss HP/state
        - Tactical patterns may be extracted into separate classes/files if this improves clarity.

Architecture goal:
Implement the boss behavior in three layers:

Layer 1: Action layer
Small atomic actions executed by VengefulSpirit.cs.

Layer 2: Tactical layer
Reusable combat patterns composed from actions.

Examples:
- CommonAttackPattern
- BlinkAttackPattern
- ChargeAttackPattern
- SpiritSwordsPattern
- SpawnShieldPattern

Each tactical pattern should:
- Have a clear start and end.
- Know when the boss is busy.
- Be cancellable/interrupted if the boss dies or the fight ends.
- Return control to the strategic layer when completed.
- Avoid starting another pattern while one is still running.
- Optionally expose metadata:
    - phase availability
    - weight
    - cooldown
    - max consecutive uses
    - whether it requires specific anchors
    - whether it is a phase transition pattern

Layer 3: Strategic layer
Implemented in VengefulSpiritAI.cs.

Responsibilities:
- Start boss behavior when fight begins.
- Track current phase.
- Switch from Phase 1 to Phase 2 based on HP threshold.
- Immediately execute the Phase 2 opening shield pattern.
- Select tactical patterns according to phase rules.
- Enforce max repetitions.
- Prevent the same attack from being spammed.
- Insert short pauses between patterns if needed.
- Avoid choosing invalid patterns.
- Stop all behavior when boss dies.

Important:
The strategic layer should NOT manually perform low-level movement, animation, hitbox, or spawn logic.
It should only choose and run tactical patterns.

Boss attacks:

Attack 1: Common Attack
Behavior:
- Boss slowly moves toward the player.
- When the player is inside the reach zone, boss raises hand and attacks.
- Plays Attack animation.
- Uses onAttack animation trigger.
- Has a simple damage window.
- The player should be damaged only during the active hit window.
- The boss may perform this attack up to 2 times in a row in both phases.
- After max 2 common attacks in a row, the boss should teleport/reposition before choosing another pattern.

Attack 2: Blink Attack
Behavior:
- Boss disappears or blinks.
- Boss teleports behind the player.
- Boss performs the common attack.
- Uses the same attack animation and damage window as Common Attack if possible.
- Phase 1: available once in a tactical cycle.
- Phase 2: can happen twice in a tactical cycle or before forced repositioning.
- Must be readable: add a short pre-attack delay or telegraph after appearing behind the player.

Attack 3: Charge Attack
Behavior:
- Boss teleports to one of two lower ground-level points.
- Starts ChargeAttack1 animation (Set animator bool isChargeAttackStarted = true)
- After ChargeAttack1 animation is complete, boss moves very fast horizontally toward the other charge point.
- During the fast horizontal movement, boss damages the player (it will have damager component).
- Play ChargeAttack2 animation while moving (Set animator bool isChargeAttackStarted = false when start move).
- At the end of the charge, boss idles for 1 second.
- Then boss disappears and teleports to a different location.
- Phase 1: unavailable.
- Phase 2: available once per tactical cycle.
- Must not damage the player before the actual charge movement begins.
- Must stop cleanly if boss dies during charge.

Attack 4: Spirit Swords
Behavior:
- Boss teleports to a higher-level casting point.
- Starts casting magic (Set animator bool isCasting = true)
- Spirit swords appear in the air at predefined anchors.
- After casting is complete, set isCasting = false.
- Boss stays for 1 second.
- Then boss disappears and spawns in another location.
- Phase 1:
    - use once.
    - spawn swords from one of two available anchors.
- Phase 2:
    - use once.
    - spawn swords from two available anchors.
- Swords should use predefined anchors, not random world positions.
- Sword behavior should be readable and avoid bullet-hell spam.

Attack 5: Spawn Shield
Behavior:
- Boss stays in the same place.
- Starts casting magic.
- Set animator bool isCasting = true.
- Spawns spectral shield.
- After casting ends, set isCasting = false.
- Boss continues moving toward the player if the player is nearby.
- Used once immediately after entering Phase 2. Before using it, boss must teleport to  central location,
  unreachable for the player.
- Spawn Shield should be treated as a tactical pattern, but it should be forced by the strategic layer
  as the opening Phase 2 transition action.
- Shield should not be respawned repeatedly unless explicitly configured.

Phases:

Phase 1:
Available patterns:
1. Common Attack
    - max 2 times in a row
    - then boss must teleport/reposition
2. Spirit Swords
    - once per cycle
    - uses one of two sword anchor groups
3. Blink Attack
    - once per cycle

Phase 2:
On entering Phase 2:
- Force Spawn Shield once in the middle of the screen.

After that, available patterns:
1. Common Attack
    - max 2 times in a row
    - then boss must teleport/reposition
2. Spirit Swords
    - once per cycle
    - uses two available sword anchor groups
3. Blink Attack
    - twice per cycle
4. Charge Attack
    - once per cycle

Define what a “cycle” means in code.
Suggested implementation:
A cycle is a strategic round where the AI tries to use each available major pattern according
to phase limits, then resets usage counters after all required/allowed patterns are exhausted
or after a forced reset condition.

Alternative acceptable implementation:
Use weighted random selection with per-pattern cooldowns and max-use counters, but still enforce:
- Common Attack max 2 in a row.
- Phase 1 Spirit Swords max once before reset.
- Phase 1 Blink Attack max once before reset.
- Phase 2 Spirit Swords max once before reset.
- Phase 2 Blink Attack max twice before reset.
- Phase 2 Charge Attack max once before reset.
- Spawn Shield once only on Phase 2 start.

Required state handling:
- Boss must have a clear “busy” state.
- AI must not start a new tactical pattern while another one is active.
- Boss death cancels current behavior.
- Phase transition cancels or waits for the current pattern safely.
- Damage windows must be controlled explicitly, not active for the whole animation.
- Teleport/reappear should not accidentally damage the player until boss completely appeared (we should have
  some delay that disables skeleton Damager when it starts teleport and enable when it reappears completely).
- If the player is missing, dead, or unreachable, boss should idle/reposition instead of throwing exceptions.
- All key values should be tunable in Inspector:
    - attack reach distance (or this can be a helper object with collider in skeleton hierarchy if it's
      more reliable and simpler)
    - move speed
    - attack telegraph delay
    - attack cooldown
    - blink offset behind player (again if additional transform in skeleton hierarchy helps here - can be done)
    - charge speed
    - charge points
    - casting points
    - sword anchors
    - sword prefab
    - shield prefab
    - phase 2 HP threshold
    - delays after attacks
    - delays before/after teleport

Animation requirements:
- Common attack:
    - Trigger: onAttack
- Charge:
    - Bool: isChargeAttackStarted
    - ChargeAttack1 before movement
    - ChargeAttack2 during movement
- Casting:
    - Bool: isCasting
- Avoid magic strings where possible.
    - Use serialized string fields or static readonly hashes.
    - Prefer Animator.StringToHash.


Suggested responsibilities split (but if there are simpler/easier in use architecture - please suggest): 

VengefulSpiritAI.cs:
- Strategic controller.
- Owns:
    - current phase
    - current running pattern
    - phase transition logic
    - pattern selection
    - counters/cooldowns
    - fight start/stop
- Calls tactical patterns, not raw animation details.

Optional pattern classes:
- IVengefulSpiritPattern
    - string Id
    - bool CanRun(VengefulSpiritAIContext context)
    - IEnumerator Run(VengefulSpiritAIContext context)
    - void ResetCycleState()
- CommonAttackPattern
- BlinkAttackPattern
- ChargeAttackPattern
- SpiritSwordsPattern
- SpawnShieldPattern

VengefulSpiritAIContext:
- References:
    - VengefulSpirit boss
    - Transform player
    - current phase
    - cancellation/death state
    - config
    - pattern counters

Deliverables:
1. Refactor or implement the boss AI using this architecture.
2. Keep VengefulSpirit.cs focused on physical execution.
3. Keep VengefulSpiritAI.cs focused on strategic decisions.
4. Extract tactical patterns into separate classes if it makes the code cleaner.
5. Add clear comments explaining the three behavior layers.
6. Make the fight tunable through serialized fields.
7. Ensure that all coroutines stop safely when the boss dies or the fight ends.
8. Ensure that phase 2 shield is spawned once immediately after phase transition.
9. Ensure that attack repetition limits are enforced.
10. Ensure that all damage windows are explicit and short.

Before coding, inspect the current implementation and preserve existing public API where reasonable.
If existing components for hitboxes, health, sword spawning, shield spawning, or animation events
already exist, integrate with them instead of inventing duplicate systems.
If some required dependency is missing, create a small adapter/stub only if necessary and clearly
mark it.