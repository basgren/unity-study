# TODO

## System
* Use Singletons for main service and check for duplicates, as entering different scenes with EntryPoint prefab 
  makes it duplicate all services. While this issue may be resolved by Bootstrap scene, but sometimes it's
  convenient to add EntryPoint to some test scene, without modifying bootstrap scene.
* Implement bootstrap scene to initialize systems and load first scene. Currently `EntryPoint` prefab is added to
  each scene.

## Hero
* attacking and immediate jump breaks animation state and player stops turning in different directions and reacting
  on Attack and Use keypresses.

## Objects
* Destructable barrels cannot be dragged in stack. They also don't have top capsule collider.
* When player opens the door and runs away, when door open animation ends, player is still teleported to
  anorther scene. This should be changed to one of following:
  * when door is being opened, disable player controls, so he stays in the interraction area.
  * OR at the moment the door is opened, check if player is still in the interaction area and if no - cancel transition.
* DOORS: if pressing fast Use button, door will reset its animation. we should ignore Use presses until animation is
    finished (disable component?).
* make coins collide with barrels when they are dropped.