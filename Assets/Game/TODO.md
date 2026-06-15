# TODO

## System
* Use Singletons for main service and check for duplicates, as entering different scenes with EntryPoint prefab 
  makes it duplicate all services. While this issue may be resolved by Bootstrap scene, but sometimes it's
  convenient to add EntryPoint to some test scene, without modifying bootstrap scene.
* Implement bootstrap scene to initialize systems and load first scene. Currently `EntryPoint` prefab is added to
  each scene.
* when transitioning between scenes, coin amount is not shown until new coin is picked up.
* when player turns Helm to enable shooting traps and then dies, player is respawned, shootong traps are disabled
  and Helm is inactive. either state should be rolled back when dies (or to the one that was upon resting), or
  shooters should remain active on reload.

## Hero
* Fix knockback on being hit. Currently horizontal knockback doesn't work, as horizontal direction is
    set in every frame and is overridden by player keypress. What should be done: when player is hit,
    take controls for some time (0.5 secs, for example) and apply knockback for this time. After that
    time return control.
* when hit and quickly throw sword, hero stops turning.
* grappling hook pivot point should be moved upper - recently hero changed pivots

## Cannon
* Add effect on fire

## Objects
* Destructable barrels cannot be dragged in stack. They also don't have top capsule collider.
* When player opens the door and runs away, when door open animation ends, player is still teleported to
  anorther scene. This should be changed to one of following:
  * when door is being opened, disable player controls, so he stays in the interraction area.
  * OR at the moment the door is opened, check if player is still in the interaction area and if no - cancel transition.
* DOORS: if pressing fast Use button, door will reset its animation. we should ignore Use presses until animation is
    finished (disable component?).
* make coins collide with barrels when they are dropped.
* debris - enhance directoion calculation - if projectile met wall, debris should burst in the direction opposite
    to wall.
* when player doesn't have hook, anchors should not be highlighted.

## Textures
* `ultra-far-bg` - has gray pixel in the top middle

## Shop
* Shop menu lacks sounds