# Stone Golem

Most important are attack description. Added possibility of attack golem for the player, to avoid situation
when player has no change to hit golem.


## Boss Attacks

### 1. Melee

Attack:
* moves to the player and hits him
* if player is close, and hitting boss, and boss is not in cooldown and not in action, boss hits player.

Position:
any position on lowel level

Opportunity for the player:
* move back/jump and hit back during cooldown

### 2. Flying hand

Attack:
* on medium distance shoots with hand, hand flies with acceleration to
  the player, rotation speed is limited, so player can avoid it. after it hits the ground,
  returns in 1-2 seconds to initial position.

Opportunity for the player:
* jump, hook, then run closely and 1-2 hits back until hand returns.

### 3. Laser beam

Attack:
* on any distance: shoots laser beam which aimed slightly in front of player and when started, moved towards player within 2 secs,
  then stops.

Opportunity for the player:
* harder. run, then if close - may hit 1-2 times.

### 4. Laser cross

More for the phase when there are no obstacles (2nd or 3rd).
* golem flies to the center of the room, fires 4 beams in 90 degrees, and they start rotating,
  when rotated by 180 degrees, beams stop and golem returns to initial position.

Opportunity for the player:
* hard. run, then use hooks to jump over

### 5. Ground hit
More for the phase when there are obstacles (1st or 2nd).

* golem combines into a big stone, then does hammer movement and hits the ground, stones fall from above.
* in 1st phase can break stone platforms.
* when transitioning to the second phase, golem


### 6. Stone wave

For later phase - when there are no obstacles.

* golem glows for several seconds and when it glows, spike appear from ground in waves.

Opportunity for the player:
* either to jump and avoid spikes to the golem, or use hook to avoid.


## Phases

In total there are 2 phases. Transition to the second phase occurs when player beats 50% of the boss health.

### Phase 1

Phase 1 is in chamber where there are two stone platforms (level 2) and ground level 1.
In phase 1 golem may move on level 1 to catch the player or perform movements. For the boss there are 3 points on
level 1 where it can perform Ground Hit and fly to level 2 (level 2 has 4 points - 2 on each platform - landing points).
Boss "jumping" to higher platforms should actually be smooth, as boss kinda flies using magic forces.
From level 2 to level 1 golem moves the same way - moves to landing point and "jumps/flies"

Boss attacks:
Melee, Flying hand, Laser beam, Ground hit.

### Phase 2

Phase 2 happens in another chamber, there are no platforms and 3 grappling hook anchors to avoid some of golem attacks.

Boss attacks:
All from Phase 1 + Laser cross, Stone wave.
