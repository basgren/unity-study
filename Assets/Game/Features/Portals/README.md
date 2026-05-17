# Portals

Connects player travel between scenes through two portal kinds:

- **Door** — interactable (button press, open/close animation). See `Doors/Door.cs`.
- **Entrance** — auto-triggered on collider enter, no animation. See `Entrance/Entrance.cs`.

Both implement the shared `IPortal` interface and serialize their destination as a single
`PortalLink` struct. All editor tools (link drawer, scene cache, validator, project updater,
change-id window, scene-reference repair) are kind-agnostic and look up the right behaviour
through `PortalKindRegistry`. Each kind self-registers from an `[InitializeOnLoad]` stub.

## Layout

```
Portals/
├── Common/                     shared types used by every portal kind
│   ├── IPortal.cs
│   ├── PortalLink.cs           single struct shared by Door + Entrance
│   ├── PortalUtils.cs          generic GetPortalsInScene<T> / FindPortalByIdInScene<T>
│   ├── PortalTravelService.cs  fade, scene load, camera snap, teleport
│   ├── PortalGizmoLabelStyle.cs
│   └── Editor/                 generic editor tooling for every kind
│       ├── PortalKind.cs              + PortalKindRegistry.cs
│       ├── PortalEntry.cs             + ScenePortalCache.cs
│       ├── PortalLinkDrawer.cs        (filters dropdown by owning kind)
│       ├── PortalProjectUpdater.cs    (rename-safe reference updater)
│       ├── PortalValidator.cs         (+ PortalValidationError)
│       ├── PortalChangeIdWindow.cs    + PortalInspectorFoldout.cs
│       ├── PortalSceneReferenceRepair.cs (+ AutoRepairPostprocessor)
│       ├── PortalEditorUtils.cs
│       ├── ScenePortalCacheInvalidator.cs
│       └── SceneReferenceDrawer.cs
├── Doors/
│   ├── Door.cs                 component (interactable trigger, gizmos, autoincrement)
│   ├── DoorWooden/             prefab + audio cues
│   ├── StoneDoor/              prefab + anim (unrelated to portal system, just lives here)
│   └── Editor/
│       ├── DoorPortalRegistration.cs  [InitializeOnLoad] registers Door kind
│       ├── DoorEditor.cs              thin: calls PortalInspectorFoldout
│       ├── DoorValidationMenu.cs      thin: calls PortalValidator with Door kind
│       ├── DoorPlayModeValidator.cs   thin: calls PortalValidator with Door kind
│       └── DoorBuildValidator.cs      thin: calls PortalValidator with Door kind
└── Entrance/
    ├── Entrance.cs             component (auto-trigger on collider, gizmos, autoincrement)
    ├── Entrance.prefab
    └── Editor/
        ├── EntrancePortalRegistration.cs  [InitializeOnLoad] registers Entrance kind
        ├── EntranceEditor.cs              thin: calls PortalInspectorFoldout
        ├── EntranceValidationMenu.cs      thin: calls PortalValidator with Entrance kind
        └── EntrancePlayModeValidator.cs   thin: calls PortalValidator with Entrance kind
```

## Adding a new portal kind

1. Create a component implementing `IPortal` (use Door/Entrance as a template — `PortalLink link`
   field, `OnValidate` autoincrement, gizmos).
2. Add an `[InitializeOnLoad]` registration stub in `<Kind>/Editor/` that calls
   `PortalKindRegistry.Register(new PortalKind(...))`.

That's it — the shared link drawer, scene cache, validator, project updater, change-id window,
and scene-reference repair tool all pick up the new kind automatically.

## IDs

- Each portal has a per-scene autoincrement integer id (stored as a numeric string for
  type-safe evolution).
- The id is auto-assigned on `OnValidate` and read-only in the inspector. Use the
  "Change ID" action inside the collapsed `Kind ID: N` foldout to rename safely — references
  in open scenes and prefabs are updated automatically.

## Editor menus

- `Tools/Portals/Repair Scene References` — refreshes cached `SceneReference.scenePath`
  values from `sceneGuid`. Runs automatically after a scene rename/move (with a prompt).
- `Tools/Portals/Doors/Validate Open Scenes` — checks every door's link in the open scenes.
- `Tools/Portals/Entrances/Validate Open Scenes` — same, for entrances.
- `Tools/Portals/Doors/Validation On Play Enabled` — toggle automatic validation just
  before entering Play. Same toggle exists under `Entrances/`.
