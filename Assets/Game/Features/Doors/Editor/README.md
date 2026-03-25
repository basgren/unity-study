# Unity Door System (Unity 2019)

This package provides a simple, maintainable way to connect doors across multiple scenes.

## Key Ideas

- Each `Door` has a stable string `DoorId` (unique **within** a scene).
- Each door contains a `DoorLink` pointing to:
  - a `SceneReference` (scene GUID based, stable across moves/renames)
  - a target `DoorId` inside that scene.

## Editor UX

- `DoorId` is read-only in the inspector.
- Use **Change ID** to rename a door id safely:
  - Updates references in **open scenes** (marks scenes dirty; does NOT auto-save scenes)
  - Updates references in **prefabs** (prefabs are auto-saved)

## Validation

- Menu: `Tools/Doors/Validate Open Scenes`
- Build-time: `DoorBuildValidator` fails the build if it finds:
  - invalid/empty DoorId
  - duplicate DoorId within a scene
  - links pointing to missing scenes or missing target doors

## Notes

- ID characters allowed: `[0-9a-zA-Z_-]`
- IDs are generated with `System.Random` (non-crypto); duplicates are expected to be extremely rare and are caught by validation.
