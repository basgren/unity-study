# TODO

* Use Singletons for main service and check for duplicates, as entering different scenes with EntryPoint prefab 
  makes it duplicate all services. While this issue may be resolved by Bootstrap scene, but sometimes it's
  convenient to add EntryPoint to some test scene, without modifying bootstrap scene.
* Implement bootstrap scene to initialize systems and load first scene. Currently `EntryPoint` prefab is added to
  each scene.

Objects
* Destructable barrels cannot be dragged in stack. They also don't have top capsule collider.