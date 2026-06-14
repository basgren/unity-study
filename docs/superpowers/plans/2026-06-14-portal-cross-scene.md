# Portal cross-scene teleport — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the `Portal` prefab able to teleport the player one-way to a point in the same scene *or* another scene, by unifying it into a single portal kind that reuses the existing `PortalTravelService` framework.

**Architecture:** Retire the bespoke `PortalController` + `PortalDestController` pair and replace it with one `Portal` component (mirroring `Entrance`) that both carries a `PortalLink` (target scene + target portal id) and is itself an id-addressable spawn destination. A Portal with a link is a source; a Portal with no link is an inert destination. Travel (fade-out → optional scene load → teleport → fade-in) is handled entirely by the already-built `PortalTravelService`; the only new runtime logic is the trigger wiring plus an empty-link guard.

**Tech Stack:** Unity 2D, C#, `Assembly-CSharp` (no per-feature asmdefs), NUnit EditMode tests under `Assets/Game/Editor/Tests/`.

**Spec:** `docs/superpowers/specs/2026-06-14-portal-cross-scene-design.md`

---

## Conventions for this plan

- **Commits are left for you to run manually** (project preference: review-then-commit). Each task ends at a reviewable checkpoint with a *suggested* commit command — run it when you're satisfied.
- **`.meta` files move with their asset.** Always `git mv` the `.cs`/`.prefab` **and** its `.cs.meta`/`.prefab.meta` together. Unity tracks assets by the GUID stored in the `.meta`; losing it breaks every prefab/scene reference.
- **Verification gate is Unity, not offline compile.** The file moves and new files are not in Unity's existing Bee `.rsp`, so the offline csc check won't see them until Unity regenerates. The reliable check is: let Unity recompile (watch the Console for errors) and run the EditMode tests via **Window ▸ General ▸ Test Runner ▸ EditMode**.
- **Brace style:** K&R, braces always (per AGENTS.md).

## File structure

| File | Action | Responsibility |
|------|--------|----------------|
| `Assets/Game/Features/Portals/Portal/Portal.cs` | move from `Assets/Game/Features/Interactive/Portal/PortalController.cs` + rewrite | Unified Portal runtime component (`IPortal`, trigger, id autoincrement, gizmos) |
| `Assets/Game/Features/Portals/Portal/Portal.prefab` | move from `Assets/Game/Features/Interactive/Portal/Portal.prefab` | Source-portal prefab template |
| `Assets/Game/Features/Portals/Portal/PortalDest.prefab` | move from `Assets/Game/Features/Interactive/Portal/PortalDest.prefab` | Destination-portal prefab (re-pointed to `Portal` component in Unity) |
| `Assets/Game/Features/Interactive/Portal/PortalDestController.cs` | delete | Removed; destination is now a link-less `Portal` |
| `Assets/Game/Features/Portals/Portal/Editor/PortalRegistration.cs` | create | Registers the `Portal` kind so shared editor tooling lights up |
| `Assets/Game/Features/Portals/Portal/Editor/PortalEditor.cs` | create | Inspector: id foldout + link drawer |
| `Assets/Game/Editor/Tests/Portal/PortalTests.cs` | create | EditMode tests for `GetEntryPosition` |

---

## Task 1: Move runtime + prefab files (preserve GUIDs)

**Files:**
- Move: `Assets/Game/Features/Interactive/Portal/PortalController.cs` → `Assets/Game/Features/Portals/Portal/Portal.cs`
- Move: `Assets/Game/Features/Interactive/Portal/Portal.prefab` → `Assets/Game/Features/Portals/Portal/Portal.prefab`
- Move: `Assets/Game/Features/Interactive/Portal/PortalDest.prefab` → `Assets/Game/Features/Portals/Portal/PortalDest.prefab`

This task only moves files (with their `.meta`) so GUIDs survive. The `.cs` still contains the old `PortalController` class after the move — that's fixed in Task 2. Do not open Unity between this task and Task 2 (the filename≠classname mismatch would log a transient error).

- [ ] **Step 1: Create the destination folder**

Run (Git Bash):
```bash
mkdir -p "Assets/Game/Features/Portals/Portal/Editor"
```

- [ ] **Step 2: git mv the runtime script + its meta**

```bash
git mv "Assets/Game/Features/Interactive/Portal/PortalController.cs" "Assets/Game/Features/Portals/Portal/Portal.cs"
git mv "Assets/Game/Features/Interactive/Portal/PortalController.cs.meta" "Assets/Game/Features/Portals/Portal/Portal.cs.meta"
```

- [ ] **Step 3: git mv both prefabs + their metas**

```bash
git mv "Assets/Game/Features/Interactive/Portal/Portal.prefab" "Assets/Game/Features/Portals/Portal/Portal.prefab"
git mv "Assets/Game/Features/Interactive/Portal/Portal.prefab.meta" "Assets/Game/Features/Portals/Portal/Portal.prefab.meta"
git mv "Assets/Game/Features/Interactive/Portal/PortalDest.prefab" "Assets/Game/Features/Portals/Portal/PortalDest.prefab"
git mv "Assets/Game/Features/Interactive/Portal/PortalDest.prefab.meta" "Assets/Game/Features/Portals/Portal/PortalDest.prefab.meta"
```

- [ ] **Step 4: Verify the moves and GUID preservation**

Run:
```bash
git status --short
grep -m1 "guid:" "Assets/Game/Features/Portals/Portal/Portal.cs.meta"
```
Expected: `git status` shows the three files (and metas) as renames (`R`). The Portal.cs.meta guid is still `c1f6186e443be4e4993b739c391d2a4a`.

- [ ] **Step 5 (checkpoint, optional commit):**

```bash
git add -A
git commit -m "refactor: move Portal scripts/prefabs into Features/Portals/Portal"
```

---

## Task 2: Rewrite `Portal.cs` as the unified Portal component

**Files:**
- Modify (full rewrite): `Assets/Game/Features/Portals/Portal/Portal.cs`

- [ ] **Step 1: Replace the file contents entirely**

Write `Assets/Game/Features/Portals/Portal/Portal.cs`:

```csharp
using System;
using System.Globalization;
using Game.Core.Services.Scene;
using Game.Core.Utils;
using Game.Features.Portals.Common;
using UnityEngine;
using UnityEngine.Events;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Features.Portals.Portal {
    /// <summary>
    /// A trigger portal that teleports the player to another portal the instant the player enters its
    /// collider. The destination may live in the same scene or another scene. Travel is one-way: a
    /// Portal with a link is a source; a Portal without a link is an inert destination / spawn point.
    /// Unlike Entrance there is no facing requirement and no walk-in/walk-out cinematic — the shared
    /// <see cref="PortalTravelService"/> default path (fade-out, optional scene load, teleport,
    /// fade-in) is used as-is.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class Portal : MonoBehaviour, IPortal {
        private const string PlayerTag = "Player";

        [SerializeField, HideInInspector]
        private string portalId;

        [Tooltip("Destination scene and portal the player is sent to. Leave empty to make this Portal " +
                 "a pure destination (inert when stepped on).")]
        [SerializeField]
        private PortalLink link;

        [Tooltip("World position the player is teleported to when arriving at this portal. " +
                 "Place it OUTSIDE the trigger collider so the player does not immediately re-trigger " +
                 "travel. If empty, this portal's own transform position is used.")]
        [SerializeField]
        private Transform entryPoint;

        [Tooltip("Invoked after the player has finished traveling to this portal. " +
                 "Hook audio cues or other arrival effects here.")]
        [SerializeField]
        private UnityEvent onEntered;

        /// <summary>Portal identifier. Numeric string ("1", "2", ...) auto-assigned per scene.</summary>
        public string PortalId => portalId;

        /// <summary>Destination link for this portal. Points to another Portal (filtered by kind in the drawer).</summary>
        public PortalLink Link => link;

        string IPortal.Id => portalId;
        SceneReference IPortal.TargetScene => link.TargetScene;
        string IPortal.TargetId => link.TargetId;

        public Vector3 GetEntryPosition() {
            if (entryPoint != null) {
                return entryPoint.position;
            }

            return transform.position;
        }

        /// <summary>
        /// Invoked after the player has arrived at this portal. Wired by designers when audio or
        /// other side effects must play on arrival.
        /// </summary>
        public void NotifyEntered() {
            onEntered?.Invoke();
        }

        private void OnTriggerEnter2D(Collider2D other) {
            if (!other.CompareTag(PlayerTag)) {
                return;
            }

            // A Portal with no link is a pure destination / spawn point. Without this guard, stepping
            // on it would fire a pointless fade-out/fade-in with no teleport.
            if (string.IsNullOrEmpty(link.TargetId)) {
                return;
            }

            // Suppress re-triggering while a travel is already in progress (e.g. the player overlaps
            // the destination collider right after arriving).
            if (PortalTravelService.IsTraveling) {
                return;
            }

            PortalTravelService.Travel(this, PortalUtils.FindPortalByIdInScene<Portal>);
        }

        private static readonly Color GizmoSpawnColor = new Color(1f, 0.85f, 0f, 0.9f);
        private static readonly Color GizmoLinkColor = new Color(0f, 1f, 0.4f, 0.7f);
        private const float GizmoCrossSceneHintLength = 1.5f;

        private void OnDrawGizmos() {
            var spawnPos = GetEntryPosition();

            // Entry marker (yellow): where the player ends up when arriving at this portal.
            Gizmos.color = GizmoSpawnColor;
            Gizmos.DrawSphere(spawnPos, 0.12f);

            if (string.IsNullOrEmpty(link.TargetId)) {
                return;
            }

            // Connection hint: line to the target collider in the same scene, or a short upward stub
            // for cross-scene targets.
            Gizmos.color = GizmoLinkColor;
            var center = GetColliderCenter();

            if (link.TargetScene.ScenePath == gameObject.scene.path) {
                var target = PortalUtils.FindPortalByIdInScene<Portal>(gameObject.scene, link.TargetId);
                if (target != null) {
                    Gizmos.DrawLine(center, target.GetColliderCenter());
                }
            } else {
                Gizmos.DrawLine(center, center + (Vector3.up * GizmoCrossSceneHintLength));
            }
        }

        private Vector3 GetColliderCenter() {
            var col = GetComponent<Collider2D>();
            if (col != null) {
                return col.bounds.center;
            }

            return transform.position;
        }

#if UNITY_EDITOR
        private void OnValidate() {
            if (Application.isPlaying) {
                return;
            }

            // Prefab asset itself never carries a per-scene id; clear it so it does not propagate to instances.
            if (PrefabUtility.IsPartOfPrefabAsset(this)) {
                if (!string.IsNullOrEmpty(portalId)) {
                    portalId = string.Empty;
                    EditorUtility.SetDirty(this);
                }

                return;
            }

            // Prefab instances inherit the source's (empty) id; reassign a fresh per-scene id.
            var source = PrefabUtility.GetCorrespondingObjectFromSource(this) as Portal;
            var isInheritedFromPrefab = source != null && string.Equals(portalId, source.portalId, StringComparison.Ordinal);

            if (string.IsNullOrEmpty(portalId) || isInheritedFromPrefab) {
                portalId = NextFreeIdInScene().ToString(CultureInfo.InvariantCulture);
                EditorUtility.SetDirty(this);
            }
        }

        /// <summary>
        /// Returns max numeric id of all Portals in this scene + 1, ignoring this instance and non-numeric ids.
        /// </summary>
        private int NextFreeIdInScene() {
            var portals = PortalUtils.GetPortalsInScene<Portal>(gameObject.scene);
            int max = 0;
            for (var i = 0; i < portals.Count; i++) {
                var other = portals[i];
                if (other == null || other == this) {
                    continue;
                }

                if (IdUtils.TryParsePortalId(other.portalId, out var otherId) && otherId > max) {
                    max = otherId;
                }
            }

            return max + 1;
        }

        public void EditorSetPortalId(string newId) {
            portalId = newId;
        }
#endif
    }
}
```

- [ ] **Step 2: Sanity-check the symbols this file depends on exist**

Run:
```bash
grep -n "TryParsePortalId" Assets/Game/Core/Utils/*.cs
grep -n "public static List<T> GetPortalsInScene" Assets/Game/Features/Portals/Common/PortalUtils.cs
grep -n "ScenePath" Assets/Game/Core/Services/Scene/SceneReference.cs
```
Expected: `IdUtils.TryParsePortalId(...)` exists, `PortalUtils.GetPortalsInScene<T>` exists, `SceneReference.ScenePath` exists. (If `SceneReference` lives in a differently-named file, glob `Assets/Game/Core/Services/Scene/*.cs` for `struct SceneReference`.)

- [ ] **Step 3: Verify in Unity**

Open the project in Unity (or let it regain focus so it recompiles). Open **Console** (clear it first). Expected: no compile errors mentioning `Portal.cs`. The prior `PortalDestController`/`PortalDest.prefab` may show a "missing script" warning — that is expected and is fixed in Tasks 3 & 6.

- [ ] **Step 4 (checkpoint, optional commit):**

```bash
git add Assets/Game/Features/Portals/Portal/Portal.cs
git commit -m "feat: unify Portal into a single IPortal kind with cross-scene travel"
```

---

## Task 3: Delete `PortalDestController`

**Files:**
- Delete: `Assets/Game/Features/Interactive/Portal/PortalDestController.cs` (+ `.meta`)

No C# references this type (only the old prefab/scene referenced it by GUID; that wiring is replaced in Task 6).

- [ ] **Step 1: Confirm no code references remain**

Run:
```bash
grep -rln "PortalDestController" Assets --include=*.cs
```
Expected: no output (empty). If anything prints, stop and resolve it before deleting.

- [ ] **Step 2: git rm the script + its meta**

```bash
git rm "Assets/Game/Features/Interactive/Portal/PortalDestController.cs" "Assets/Game/Features/Interactive/Portal/PortalDestController.cs.meta"
```

- [ ] **Step 3: Remove the now-empty source folder if Unity left it**

```bash
rmdir "Assets/Game/Features/Interactive/Portal" 2>/dev/null && git rm -f "Assets/Game/Features/Interactive/Portal.meta" 2>/dev/null || true
```
(If the folder still holds other files, this no-ops — that is fine.)

- [ ] **Step 4 (checkpoint, optional commit):**

```bash
git add -A
git commit -m "refactor: remove PortalDestController (destination is now a link-less Portal)"
```

---

## Task 4: Register the Portal kind

**Files:**
- Create: `Assets/Game/Features/Portals/Portal/Editor/PortalRegistration.cs`

This single `[InitializeOnLoad]` registration makes the shared `PortalLinkDrawer` (dropdown), `PortalValidator`, change-id window, and scene-reference repair all work for `Portal` — identical to how `Entrance` registers.

- [ ] **Step 1: Create the registration file**

Write `Assets/Game/Features/Portals/Portal/Editor/PortalRegistration.cs`:

```csharp
#if UNITY_EDITOR
using Game.Features.Portals.Common;
using Game.Features.Portals.Common.Editor;
using UnityEditor;

namespace Game.Features.Portals.Portal.Editor {
    /// <summary>
    /// Registers the Portal kind with <see cref="PortalKindRegistry"/> so the shared editor tools
    /// (link drawer, validator, change-id window, scene-reference repair) resolve Portal without
    /// Common code referencing the concrete type.
    /// </summary>
    [InitializeOnLoad]
    public static class PortalRegistration {
        static PortalRegistration() {
            PortalKindRegistry.Register(new PortalKind(
                typeof(Portal),
                "Portal",
                scene => PortalUtils.GetPortalsInScene<Portal>(scene),
                (scene, id) => PortalUtils.FindPortalByIdInScene<Portal>(scene, id)
            ));
        }
    }
}
#endif
```

- [ ] **Step 2: Verify in Unity**

Let Unity recompile. Expected: no Console errors. (Full verification of the dropdown happens in Task 6.)

- [ ] **Step 3 (checkpoint, optional commit):**

```bash
git add Assets/Game/Features/Portals/Portal/Editor/PortalRegistration.cs
git commit -m "feat: register Portal kind for shared portal editor tooling"
```

---

## Task 5: Portal inspector

**Files:**
- Create: `Assets/Game/Features/Portals/Portal/Editor/PortalEditor.cs`

- [ ] **Step 1: Create the custom editor**

Write `Assets/Game/Features/Portals/Portal/Editor/PortalEditor.cs`:

```csharp
#if UNITY_EDITOR
using Game.Features.Portals.Common.Editor;
using UnityEditor;

namespace Game.Features.Portals.Portal.Editor {
    /// <summary>
    /// Portal inspector. The id foldout is the shared <see cref="PortalInspectorFoldout"/>; this
    /// editor wires the Portal-specific setter and then draws the remaining serialized fields
    /// (including the PortalLink, which gets the shared dropdown drawer).
    /// </summary>
    [CustomEditor(typeof(Portal))]
    public sealed class PortalEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            var portal = (Portal)target;

            PortalInspectorFoldout.DrawIdFoldout(portal, "Portal", portal.PortalId, portal.EditorSetPortalId);

            EditorGUILayout.Space();

            DrawPropertiesExcluding(serializedObject, "m_Script", "portalId");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
```

- [ ] **Step 2: Verify in Unity**

Let Unity recompile. Select the `Portal.prefab` asset. Expected: the inspector shows a "Portal ID: …" foldout, a `Link` field with a **Target Scene** picker and a **Target Portal** dropdown row, plus `Entry Point` and `On Entered`. No Console errors.

- [ ] **Step 3 (checkpoint, optional commit):**

```bash
git add Assets/Game/Features/Portals/Portal/Editor/PortalEditor.cs
git commit -m "feat: add Portal inspector with id foldout and link dropdown"
```

---

## Task 6: Re-wire prefabs + IntroLevel, then play-test (manual Unity)

This is hand-work in the Unity Editor; there is no scripted substitute (we do not hand-edit scene/prefab YAML). Do every sub-step in Unity.

**Files (edited via the Editor, saved by Unity):**
- `Assets/Game/Features/Portals/Portal/Portal.prefab`
- `Assets/Game/Features/Portals/Portal/PortalDest.prefab`
- `Assets/Game/Scenes/IntroLevel.unity`

- [ ] **Step 1: Fix `PortalDest.prefab`**

Open `PortalDest.prefab`. It will show a **Missing (Mono Script)** component (the deleted `PortalDestController`). Remove that missing component and **Add Component ▸ Portal**. Leave its `Link` **empty** (this makes it a pure, inert destination). Ensure it still has a `Collider2D` set to **Is Trigger** (the `[RequireComponent(typeof(Collider2D))]` will add one if missing). Optionally assign an `Entry Point` child if the spawn should differ from the object's position. Save.

- [ ] **Step 2: Confirm `Portal.prefab` is clean**

Open `Portal.prefab`. It should show the `Portal` component (GUID preserved). The old `portalDest` reference is gone; the new fields show defaults. Leave `Link` empty on the template (links are set per instance). Save if Unity marked it dirty.

- [ ] **Step 3: Re-wire the IntroLevel instances**

Open `Assets/Game/Scenes/IntroLevel.unity`. Find the existing Portal (source) and PortalDest (destination) objects. Both should now be `Portal` components with auto-assigned ids (e.g. `1` and `2`). On the **source** Portal:
- Set `Link ▸ Target Scene` to **IntroLevel** (same-scene teleport, preserving current behavior).
- Set `Link ▸ Target Portal` (dropdown) to the **destination** Portal's id.

Leave the destination Portal's `Link` empty. Save the scene.

- [ ] **Step 4: Run the shared validator**

Run menu **Tools ▸ Portals ▸ Repair Scene References** (rebuilds cached scene paths), then re-open IntroLevel. In the source Portal inspector, confirm the Target dropdown shows the destination by name (no "Missing target portal" warning).

- [ ] **Step 5: Same-scene play-test**

Enter Play mode in IntroLevel. Walk the player into the source Portal. Expected: screen fades out, player is repositioned to the destination Portal's entry position, screen fades in, hero controls return. Walking onto the destination Portal does **nothing** (inert). No Console errors; no infinite re-trigger loop.

- [ ] **Step 6: Cross-scene smoke test**

Pick (or create) a second scene that already contains a `Portal` (add one if none: drop `Portal.prefab`, note its id). Temporarily point the IntroLevel source Portal's `Link ▸ Target Scene` at that scene and `Target Portal` at that portal's id. Ensure both scenes are in **Build Settings ▸ Scenes In Build** (cross-scene load requires it). Enter Play mode, walk into the source Portal. Expected: fade-out → target scene loads → player spawns at the target Portal's entry position → fade-in. Then **revert** the IntroLevel link back to the same-scene destination from Step 3 (unless cross-scene was the intended IntroLevel wiring) and save.

- [ ] **Step 7: Run EditMode tests**

Open **Window ▸ General ▸ Test Runner ▸ EditMode**, **Run All**. Expected: existing suites still green; the new `PortalTests` (Task 7) green once added. (If doing tasks in order, Task 7 adds these — run after Task 7.)

- [ ] **Step 8 (checkpoint, optional commit):**

```bash
git add Assets/Game/Features/Portals/Portal/Portal.prefab Assets/Game/Features/Portals/Portal/PortalDest.prefab Assets/Game/Scenes/IntroLevel.unity
git commit -m "chore: re-wire Portal/PortalDest prefabs and IntroLevel to unified Portal"
```

---

## Task 7: EditMode tests for spawn position

**Files:**
- Create: `Assets/Game/Editor/Tests/Portal/PortalTests.cs`

These cover the pure, deterministic part of the spawn behavior (`GetEntryPosition`) — the part that decides *where* the player lands. Collision/scene-travel wiring is covered by the manual play-test in Task 6 (it needs the physics + screen + scene-load services, which require Play mode rather than EditMode).

- [ ] **Step 1: Write the tests**

Write `Assets/Game/Editor/Tests/Portal/PortalTests.cs`:

```csharp
using Game.Features.Portals.Portal;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Tests.Portal {
    public class PortalTests {
        private static Portal NewPortal() {
            var go = new GameObject("Portal", typeof(BoxCollider2D));
            return go.AddComponent<Portal>();
        }

        [Test]
        public void GetEntryPosition_NoEntryPoint_ReturnsOwnPosition() {
            var portal = NewPortal();
            portal.transform.position = new Vector3(3f, 4f, 0f);

            Assert.AreEqual(new Vector3(3f, 4f, 0f), portal.GetEntryPosition());

            Object.DestroyImmediate(portal.gameObject);
        }

        [Test]
        public void GetEntryPosition_WithEntryPoint_ReturnsEntryPointPosition() {
            var portal = NewPortal();
            portal.transform.position = new Vector3(3f, 4f, 0f);

            var entry = new GameObject("Entry").transform;
            entry.position = new Vector3(10f, 20f, 0f);

            // entryPoint is a private serialized field; set it via SerializedObject like the editor would.
            var so = new SerializedObject(portal);
            so.FindProperty("entryPoint").objectReferenceValue = entry;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(new Vector3(10f, 20f, 0f), portal.GetEntryPosition());

            Object.DestroyImmediate(entry.gameObject);
            Object.DestroyImmediate(portal.gameObject);
        }
    }
}
```

- [ ] **Step 2: Run the tests in Unity**

Let Unity import the new file. Open **Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All**.
Expected: `PortalTests.GetEntryPosition_NoEntryPoint_ReturnsOwnPosition` and `...WithEntryPoint_ReturnsEntryPointPosition` both **PASS**, and no other suite regresses.

- [ ] **Step 3 (checkpoint, optional commit):**

```bash
git add Assets/Game/Editor/Tests/Portal/PortalTests.cs
git commit -m "test: add EditMode coverage for Portal.GetEntryPosition"
```

---

## Final verification checklist

- [ ] Unity Console is error-free after a clean recompile.
- [ ] EditMode Test Runner: all green (including new `PortalTests`).
- [ ] IntroLevel same-scene teleport works with fade; destination Portal is inert.
- [ ] Cross-scene teleport works (target scene loads, player spawns at target entry, fade-in) — both scenes in Build Settings.
- [ ] Portal inspector shows id foldout + Target Scene picker + Target Portal dropdown.
- [ ] `git status` shows the moves as renames (history preserved); no stray `.meta` left behind or orphaned.

## Out of scope / follow-ups

- No `PortalValidationMenu` / `PortalPlayModeValidator` for Portal (dropped by decision; the shared `PortalValidator` still runs via the kind registration where invoked generically).
- Other scenes that may want Portals are not migrated here — only `IntroLevel` had an instance.
- A `docs/system/portal.md` write-up can be added once the feature is verified (project convention for shipped systems).
```

