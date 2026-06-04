using System;
using System.Collections;
using System.Globalization;
using Game.Core.Bootstrap;
using Game.Core.Services.Scene;
using Game.Core.Utils;
using Game.Features.Characters.Hero;
using Game.Features.Hero;
using Game.Features.Portals.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Features.Portals.Entrance {
    /// <summary>
    /// Direction the hero moves when crossing an <see cref="EntranceHorizontal"/>. Defines the
    /// source-side suction direction and the destination-side spawn placement / arrival behavior.
    /// </summary>
    public enum HorizontalPortalDirection {
        Up,
        Down,
    }

    /// <summary>
    /// Horizontal variant of <see cref="Entrance"/>: the trigger sits at the top or bottom of a
    /// scene and the player crosses it vertically.
    ///
    /// Source side is identical for both directions: the portal does NOT manipulate the hero's
    /// velocity. The hero's own jump (for Up portals) or fall (for Down portals) carries him
    /// through the trigger; travel begins as soon as the hero exits the collider.
    ///
    /// Destination side branches on <see cref="HorizontalPortalDirection"/>:
    ///
    /// • <b>Up</b> — on arrival the hero spawns offscreen below the destination at
    ///   <see cref="backSpawnOffset"/> and performs a parabolic launch jump onto one of up to two
    ///   target landing points. The target is selected by the hero's facing on the SOURCE side
    ///   (right facing → <see cref="rightTarget"/>, left facing → <see cref="leftTarget"/>);
    ///   when only one is enabled the hero always lands on it and faces toward it.
    ///
    /// • <b>Down</b> — on arrival the hero spawns above the destination — the same
    ///   <see cref="backSpawnOffset"/> is used but its Y is flipped — and is released without
    ///   any scripted jump; gravity drops him into the scene. Target points are ignored.
    ///
    /// Landing points are local-space offsets — not child Transforms — so they don't appear in
    /// the prefab hierarchy and can't be accidentally re-parented or overridden per-instance.
    /// They are draggable in the Scene view via the custom inspector handles.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class EntranceHorizontal : MonoBehaviour, IPortal {
        private const string PlayerTag = "Player";
        private const float SourceExitTimeoutSeconds = 4f;
        private const float ArrivalLandingTimeoutSeconds = 6f;
        private const float ArrivalLandingHorizontalReach = 0.6f;
        private const float ArrivalLandingGracePeriod = 0.1f;

        /// <summary>
        /// Local-space landing point. Enabled flag lets a designer turn off one of the two slots
        /// when only a single landing target is desired.
        /// </summary>
        [Serializable]
        public struct TargetPoint {
            public bool enabled;
            public Vector2 offset;
        }

        [SerializeField, HideInInspector]
        private string entranceId;

        [Tooltip("Destination scene and horizontal entrance the player is sent to when entering this trigger. " +
                 "Horizontal entrances can only target other horizontal entrances.")]
        [SerializeField]
        private PortalLink link;

        [Tooltip("Direction the hero moves when crossing this portal. The source side does not " +
                 "manipulate velocity in either case — the hero's own jump or fall carries him through. " +
                 "UP: on arrival the hero spawns offscreen below the destination and performs a " +
                 "parabolic launch jump onto a target. DOWN: on arrival the hero spawns above the " +
                 "destination and gravity drops him in.")]
        [SerializeField]
        private HorizontalPortalDirection direction = HorizontalPortalDirection.Up;

        [Tooltip("Local-space offset of the offscreen spawn position used on ARRIVAL. Set this for the " +
                 "UP direction (typically below the portal, e.g. (0, -3)). When Direction is DOWN, the Y " +
                 "component is automatically mirrored so the same value places the spawn above the portal.")]
        [SerializeField]
        private Vector2 backSpawnOffset = new Vector2(0f, -3f);

        [Tooltip("UP-only. Landing point used when the source-side hero entered the previous entrance facing LEFT. " +
                 "Disable this slot to force the hero onto the right target regardless of approach direction.")]
        [SerializeField]
        private TargetPoint leftTarget = new TargetPoint { enabled = true, offset = new Vector2(-1.5f, 0f) };

        [Tooltip("UP-only. Landing point used when the source-side hero entered the previous entrance facing RIGHT. " +
                 "Disable this slot to force the hero onto the left target regardless of approach direction.")]
        [SerializeField]
        private TargetPoint rightTarget = new TargetPoint { enabled = true, offset = new Vector2(1.5f, 0f) };

        [Tooltip("UP-only. Initial upward velocity (m/s) of the arrival launch jump. Should be tall enough that the " +
                 "natural parabolic arc clears the chosen target point — the editor draws a preview arc.")]
        [SerializeField]
        private float arrivalJumpUpSpeed = 14f;

        [Tooltip("Invoked after the hero has finished arriving (touched down on the chosen target point).")]
        [SerializeField]
        private UnityEvent onEntered;

        // Carries the hero's facing across the scene load so the destination knows which target to pick.
        // Static because Entrance + Hero + new scene's EntranceHorizontal don't share state via Travel hooks.
        private static int? pendingArrivalDirSign;

        // Set by OnTriggerExit2D so the source-side coroutine can detect when the hero has cleared
        // the portal trigger (used for both Up suction and Down fall-through).
        private bool playerExitedCollider;

        /// <summary>
        /// Per-scene autoincremented numeric id ("1", "2", ...). Edit via the inspector "Change ID" action.
        /// </summary>
        public string EntranceId => entranceId;

        /// <summary>
        /// Destination link for this entrance (filtered by <see cref="EntranceHorizontal"/> kind in the drawer).
        /// </summary>
        public PortalLink Link => link;

        string IPortal.Id => entranceId;
        SceneReference IPortal.TargetScene => link.TargetScene;
        string IPortal.TargetId => link.TargetId;

        /// <summary>
        /// Entry position is the portal's own transform — used as a fallback only; the standard arrival
        /// path goes through <see cref="GetBackSpawnPosition"/> + the launch arc.
        /// </summary>
        public Vector3 GetEntryPosition() {
            return transform.position;
        }

        /// <summary>
        /// Direction this portal moves the hero in (Up or Down).
        /// </summary>
        public HorizontalPortalDirection Direction => direction;

        /// <summary>
        /// World-space offscreen spawn position used on the destination side of arrival.
        /// For DOWN portals the configured Y offset is automatically mirrored, so the spawn ends
        /// up above the portal (where the hero needs to materialize before falling in).
        /// </summary>
        public Vector3 GetBackSpawnPosition() {
            return transform.TransformPoint(GetEffectiveBackSpawnOffset());
        }

        private Vector2 GetEffectiveBackSpawnOffset() {
            return direction == HorizontalPortalDirection.Down
                ? new Vector2(backSpawnOffset.x, -backSpawnOffset.y)
                : backSpawnOffset;
        }

        /// <summary>
        /// True when the left target slot is enabled.
        /// </summary>
        public bool HasLeftTarget => leftTarget.enabled;

        /// <summary>
        /// True when the right target slot is enabled.
        /// </summary>
        public bool HasRightTarget => rightTarget.enabled;

        /// <summary>
        /// World-space position of the left target landing point (regardless of enabled flag).
        /// </summary>
        public Vector3 GetLeftTargetPosition() {
            return transform.TransformPoint(leftTarget.offset);
        }

        /// <summary>
        /// World-space position of the right target landing point (regardless of enabled flag).
        /// </summary>
        public Vector3 GetRightTargetPosition() {
            return transform.TransformPoint(rightTarget.offset);
        }

        /// <summary>
        /// Invoked after the hero has arrived at this entrance. Wired by designers when audio
        /// or other side effects must play on arrival.
        /// </summary>
        public void NotifyEntered() {
            onEntered?.Invoke();
        }

        private void OnTriggerEnter2D(Collider2D other) {
            if (!other.CompareTag(PlayerTag)) {
                return;
            }

            // Block re-triggering during a travel — e.g. the hero passing through this portal's
            // collider while jumping up on arrival.
            if (PortalTravelService.IsTraveling) {
                return;
            }

            var hooks = new PortalTravelHooks {
                BeforeFadeOut = SourceExitCoroutine,
                ResolveSpawnPosition = ResolveDestinationSpawn,
                AfterArrival = ArrivalCoroutine,
            };
            PortalTravelService.Travel(this, PortalUtils.FindPortalByIdInScene<EntranceHorizontal>, hooks);
        }

        private void OnTriggerExit2D(Collider2D other) {
            if (other.CompareTag(PlayerTag)) {
                playerExitedCollider = true;
            }
        }

        private static Vector3 ResolveDestinationSpawn(IPortal target) {
            if (target is EntranceHorizontal dest) {
                return dest.GetBackSpawnPosition();
            }

            return target.GetEntryPosition();
        }

        private IEnumerator SourceExitCoroutine() {
            var hero = G.Hero.Controller;
            if (hero == null) {
                yield break;
            }

            // Remember which way the hero was facing — the destination uses this to pick a target
            // when two landing points are configured (Up mode).
            pendingArrivalDirSign = hero.GetFacingDirSign();

            // Source side never manipulates the hero's velocity WHILE he's crossing the trigger
            // (for either direction). The hero's own momentum — his jump for Up portals, his fall
            // for Down portals — carries him through. Touching velocity here used to add an
            // unwanted "secondary hop" / "levitate" feel when the hero entered the trigger near
            // the apex of his jump or from above going down.
            playerExitedCollider = false;
            var elapsed = 0f;
            while (!playerExitedCollider && elapsed < SourceExitTimeoutSeconds) {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Once the hero has CLEARED the trigger, freeze him: zero velocity + gravity off. The
            // fade-out plus scene unload spans a few frames, and we don't want him drifting into a
            // pit or onto a hazard underneath the portal during that window. ArrivalCoroutine lifts
            // this freeze on the destination side so it never leaks past travel.
            hero.BeginScriptedFlight(disableGravity: true);
            hero.SetVelocity(Vector2.zero);
        }

        private static IEnumerator ArrivalCoroutine(IPortal target) {
            var hero = G.Hero.Controller;
            if (hero == null) {
                yield break;
            }

            // Always lift the source-side freeze first — even on the early-return paths below — so
            // the hero never gets stranded with gravity off if anything in travel goes sideways.
            hero.EndScriptedFlight();

            var dest = target as EntranceHorizontal;
            if (dest == null) {
                yield break;
            }

            var dirSign = pendingArrivalDirSign ?? hero.GetFacingDirSign();
            pendingArrivalDirSign = null;

            if (dest.direction == HorizontalPortalDirection.Down) {
                yield return DownArrivalCoroutine(dest, hero, dirSign);
            } else {
                yield return UpArrivalCoroutine(dest, hero, dirSign);
            }
        }

        private static IEnumerator UpArrivalCoroutine(EntranceHorizontal dest, PlayerController hero, int dirSign) {
            if (!dest.TryGetLandingPoint(dirSign, out var landingWorldPos, out var landingFacingSign)) {
                // No targets configured — bail out and let PortalTravelService restore control as usual.
                yield break;
            }

            hero.SetFacing(landingFacingSign);

            // Drive the hero by velocity for the arc; gravity must be ON so the trajectory feels natural.
            hero.BeginScriptedFlight(disableGravity: false);
            var spawnPos = (Vector2)hero.transform.position;
            var launchVelocity = ComputeArcVelocity(spawnPos, landingWorldPos, dest.arrivalJumpUpSpeed);
            hero.SetVelocity(launchVelocity);

            // Brief grace before checking IsGrounded — right after the teleport-to-back-spawn the
            // hero's ground checker may report grounded for a frame against nearby geometry.
            var grace = 0f;
            while (grace < ArrivalLandingGracePeriod) {
                grace += Time.deltaTime;
                yield return null;
            }

            var elapsed = 0f;
            while (elapsed < ArrivalLandingTimeoutSeconds) {
                var heroPos = (Vector2)hero.transform.position;
                var horizontalDist = Mathf.Abs(heroPos.x - landingWorldPos.x);

                if (hero.IsGrounded && horizontalDist <= ArrivalLandingHorizontalReach) {
                    break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Zero horizontal drift before handing control back, but keep any current downward velocity
            // so the hero settles on the platform naturally.
            var vy = Mathf.Min(hero.GetVerticalVelocity(), 0f);
            hero.SetVelocity(new Vector2(0f, vy));
            hero.EndScriptedFlight();
        }

        private static IEnumerator DownArrivalCoroutine(EntranceHorizontal dest, PlayerController hero, int dirSign) {
            // Preserve continuity: hero looks the same way he did when entering the source portal.
            hero.SetFacing(dirSign);

            // Reset the destination's exit flag — we use it to detect when the falling hero has
            // cleared the trigger so IsTraveling can drop without immediately re-firing the portal.
            dest.playerExitedCollider = false;

            // No scripted velocity: gravity drops the hero from the back-spawn (above the portal)
            // through the trigger and onto the scene below. Keep IsTraveling true until he leaves
            // the trigger or lands or we time out, so the destination collider doesn't re-trigger.
            var elapsed = 0f;
            while (elapsed < ArrivalLandingTimeoutSeconds) {
                if (dest.playerExitedCollider || hero.IsGrounded) {
                    break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private bool TryGetLandingPoint(int dirSign, out Vector3 worldPos, out int facingSign) {
            if (leftTarget.enabled && rightTarget.enabled) {
                var chosen = dirSign < 0 ? leftTarget : rightTarget;
                worldPos = transform.TransformPoint(chosen.offset);
                facingSign = chosen.offset.x >= 0f ? 1 : -1;
                return true;
            }

            if (leftTarget.enabled) {
                worldPos = transform.TransformPoint(leftTarget.offset);
                facingSign = leftTarget.offset.x >= 0f ? 1 : -1;
                return true;
            }

            if (rightTarget.enabled) {
                worldPos = transform.TransformPoint(rightTarget.offset);
                facingSign = rightTarget.offset.x >= 0f ? 1 : -1;
                return true;
            }

            worldPos = transform.position;
            facingSign = 1;
            return false;
        }

        /// <summary>
        /// Solves the parabolic-launch problem: given start, target and the initial upward speed,
        /// returns the (vx, vy) needed to pass through the target under <c>Physics2D.gravity</c>.
        /// Picks the descending root so the hero arcs over the platform and lands from above.
        /// </summary>
        private static Vector2 ComputeArcVelocity(Vector2 from, Vector2 to, float v0y) {
            var g = Physics2D.gravity.y; // typically -9.81
            var dx = to.x - from.x;
            var dy = to.y - from.y;

            // y(t) = v0y * t + 0.5 * g * t^2 = dy
            // 0.5 * g * t^2 + v0y * t - dy = 0
            // t = (-v0y ± sqrt(v0y^2 + 2*g*dy)) / g
            var discriminant = (v0y * v0y) + (2f * g * dy);

            float t;
            if (discriminant < 0f) {
                // Apex below target Y — launch speed is too low to reach. Fall back to a best-effort time.
                t = Mathf.Max(0.2f, Mathf.Abs(dy) / Mathf.Max(0.1f, v0y));
            } else {
                var sqrt = Mathf.Sqrt(discriminant);
                var t1 = (-v0y + sqrt) / g;
                var t2 = (-v0y - sqrt) / g;
                t = Mathf.Max(t1, t2); // descending root
                if (t <= 0f) {
                    t = 0.2f;
                }
            }

            return new Vector2(dx / t, v0y);
        }

        // -------- Gizmos --------

        private static readonly Color GizmoBackSpawnColor = new Color(0.4f, 0.6f, 1f, 0.9f);
        private static readonly Color GizmoTargetColor = new Color(1f, 0.85f, 0f, 0.9f);
        private static readonly Color GizmoDisabledTargetColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        private static readonly Color GizmoArcColor = new Color(1f, 0.85f, 0f, 0.4f);
        private static readonly Color GizmoLinkColor = new Color(0f, 1f, 0.4f, 0.7f);
        private const float GizmoCrossSceneHintLength = 1.5f;

        private void OnDrawGizmos() {
            var center = GetColliderCenter();

            // Back spawn: where the hero materializes on arrival (above for Down portals, below for Up).
            var spawnPos = GetBackSpawnPosition();
            Gizmos.color = GizmoBackSpawnColor;
            Gizmos.DrawWireSphere(spawnPos, 0.15f);
            Gizmos.DrawLine(center, spawnPos);

            // Targets + launch arcs only make sense for Up portals; in Down mode the hero just falls.
            if (direction == HorizontalPortalDirection.Up) {
                DrawTargetGizmo(leftTarget);
                DrawTargetGizmo(rightTarget);

                if (leftTarget.enabled) {
                    DrawArcGizmo(spawnPos, transform.TransformPoint(leftTarget.offset));
                }

                if (rightTarget.enabled) {
                    DrawArcGizmo(spawnPos, transform.TransformPoint(rightTarget.offset));
                }
            } else {
                // Down: hint the fall path with a straight line from spawn to portal.
                Gizmos.color = GizmoArcColor;
                Gizmos.DrawLine(spawnPos, center);
            }

            // Connection hint to the linked target portal (same as Entrance.cs).
            if (string.IsNullOrEmpty(link.TargetId)) {
                return;
            }

            Gizmos.color = GizmoLinkColor;
            if (link.TargetScene.ScenePath == gameObject.scene.path) {
                var targetEntrance = PortalUtils.FindPortalByIdInScene<EntranceHorizontal>(gameObject.scene, link.TargetId);
                if (targetEntrance != null) {
                    Gizmos.DrawLine(center, targetEntrance.GetColliderCenter());
                }
            } else {
                Gizmos.DrawLine(center, center + (Vector3.up * GizmoCrossSceneHintLength));
            }
        }

        private void DrawTargetGizmo(TargetPoint t) {
            var pos = transform.TransformPoint(t.offset);
            Gizmos.color = t.enabled ? GizmoTargetColor : GizmoDisabledTargetColor;
            Gizmos.DrawSphere(pos, 0.12f);
        }

        private void DrawArcGizmo(Vector3 from, Vector3 to) {
            var v = ComputeArcVelocity(from, to, arrivalJumpUpSpeed);
            var g = Physics2D.gravity.y;
            Gizmos.color = GizmoArcColor;

            const int segments = 24;
            // Use the same descending root for the total flight time so the preview ends at the target.
            var discriminant = (v.y * v.y) + (2f * g * (to.y - from.y));
            float totalTime;
            if (discriminant < 0f) {
                totalTime = Mathf.Max(0.2f, Mathf.Abs(to.y - from.y) / Mathf.Max(0.1f, v.y));
            } else {
                var sqrt = Mathf.Sqrt(discriminant);
                totalTime = Mathf.Max((-v.y + sqrt) / g, (-v.y - sqrt) / g);
                if (totalTime <= 0f) {
                    totalTime = 0.2f;
                }
            }

            var prev = from;
            for (var i = 1; i <= segments; i++) {
                var t = (totalTime * i) / segments;
                var x = from.x + (v.x * t);
                var y = from.y + (v.y * t) + (0.5f * g * t * t);
                var next = new Vector3(x, y, from.z);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        private void OnDrawGizmosSelected() {
            if (string.IsNullOrEmpty(link.TargetId)) {
                return;
            }

#if UNITY_EDITOR
            var center = GetColliderCenter();
            var labelPos = center + (Vector3.up * GizmoCrossSceneHintLength);

            string text;
            var targetLabel = "#" + link.TargetId;
            if (link.TargetScene.ScenePath == gameObject.scene.path) {
                text = "→ " + targetLabel;
            } else {
                var sceneName = link.TargetScene.GetSceneName();
                if (string.IsNullOrWhiteSpace(sceneName)) {
                    return;
                }

                text = "→ " + sceneName + "\n" + targetLabel;
            }

            var style = PortalGizmoLabelStyle.Get();
            var content = new GUIContent(text);
            var size = style.CalcSize(content);
            var padding = new Vector2(12f, 8f);

            var guiPoint = HandleUtility.WorldToGUIPoint(labelPos);

            var rect = new Rect(
                guiPoint.x - (size.x + padding.x) * 0.5f,
                guiPoint.y - (size.y + padding.y) * 0.5f,
                size.x + padding.x,
                size.y + padding.y
            );

            Handles.BeginGUI();

            var labelRect = new Rect(
                rect.x + padding.x * 0.5f,
                rect.y + padding.y * 0.5f,
                rect.width - padding.x,
                rect.height - padding.y
            );

            EditorGUI.LabelField(labelRect, text, style);
            Handles.EndGUI();
#endif
        }

        /// <summary>
        /// Returns the collider's bounds center, falling back to the transform position when no
        /// collider is attached yet (e.g. at prefab editing time before validation runs).
        /// </summary>
        public Vector3 GetColliderCenter() {
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

            // Prefab asset itself never carries a per-scene id; clear so it does not propagate to instances.
            if (PrefabUtility.IsPartOfPrefabAsset(this)) {
                if (!string.IsNullOrEmpty(entranceId)) {
                    entranceId = string.Empty;
                    EditorUtility.SetDirty(this);
                }

                return;
            }

            var source = PrefabUtility.GetCorrespondingObjectFromSource(this) as EntranceHorizontal;
            var isInheritedFromPrefab = source != null && string.Equals(entranceId, source.entranceId, StringComparison.Ordinal);

            if (string.IsNullOrEmpty(entranceId) || isInheritedFromPrefab) {
                entranceId = NextFreeIdInScene().ToString(CultureInfo.InvariantCulture);
                EditorUtility.SetDirty(this);
            }
        }

        /// <summary>
        /// Returns max numeric id of all horizontal entrances in this scene + 1, ignoring this
        /// instance and non-numeric ids.
        /// </summary>
        private int NextFreeIdInScene() {
            var entrances = PortalUtils.GetPortalsInScene<EntranceHorizontal>(gameObject.scene);
            int max = 0;
            for (var i = 0; i < entrances.Count; i++) {
                var other = entrances[i];
                if (other == null || other == this) {
                    continue;
                }

                if (IdUtils.TryParsePortalId(other.entranceId, out var otherId) && otherId > max) {
                    max = otherId;
                }
            }

            return max + 1;
        }

        public void EditorSetEntranceId(string newId) {
            entranceId = newId;
        }

        // Exposed for the custom inspector's Scene-view handles.
        public Vector2 EditorGetBackSpawnOffset() {
            return backSpawnOffset;
        }

        public void EditorSetBackSpawnOffset(Vector2 value) {
            backSpawnOffset = value;
        }

        public Vector2 EditorGetLeftTargetOffset() {
            return leftTarget.offset;
        }

        public void EditorSetLeftTargetOffset(Vector2 value) {
            leftTarget.offset = value;
        }

        public Vector2 EditorGetRightTargetOffset() {
            return rightTarget.offset;
        }

        public void EditorSetRightTargetOffset(Vector2 value) {
            rightTarget.offset = value;
        }
#endif
    }
}
