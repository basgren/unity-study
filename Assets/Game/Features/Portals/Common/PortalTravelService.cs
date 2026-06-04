using System;
using System.Collections;
using Game.Core.Bootstrap;
using Game.Core.Services.Scene;
using Game.Features.Characters.Hero;
using Game.Features.Hero;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Features.Portals.Common {
    /// <summary>
    /// Optional callbacks a portal kind can pass to <see cref="PortalTravelService.Travel"/> to
    /// extend the default pipeline with cinematic phases.
    /// </summary>
    public sealed class PortalTravelHooks {
        /// <summary>
        /// Runs BEFORE the fade-out. Hero controls + damage are already disabled.
        /// Use to play a source-side animation (e.g. walking into a portal). The fade-out
        /// is delayed until the returned coroutine finishes.
        /// </summary>
        public Func<IEnumerator> BeforeFadeOut;

        /// <summary>
        /// Overrides where the player teleports on the destination side. Receives the destination
        /// portal. Default = <see cref="IPortal.GetEntryPosition"/>.
        /// </summary>
        public Func<IPortal, Vector3> ResolveSpawnPosition;

        /// <summary>
        /// Runs in parallel with the destination fade-in. Use to play an arrival animation
        /// (e.g. walking from a back-spawn out to the entry point). Hero controls + damage stay
        /// disabled and <see cref="PortalTravelService.IsTraveling"/> stays true until this
        /// coroutine finishes, so the destination's own collider does not re-trigger travel.
        /// </summary>
        public Func<IPortal, IEnumerator> AfterArrival;
    }

    /// <summary>
    /// Generic travel service shared by all portal kinds (Doors, Entrances, ...).
    /// Owns the fade, scene load and player teleport logic. Type-specific lookup is provided
    /// by the caller via a finder delegate so this service never depends on a concrete portal type.
    /// Cinematic kinds may pass <see cref="PortalTravelHooks"/> to add pre/post phases.
    /// </summary>
    public static class PortalTravelService {
        private const float FadeDuration = 0.25f;
        private const int MaxCameraSnapWaitFrames = 8;

        /// <summary>
        /// Locates a portal with the given id in the specified scene.
        /// </summary>
        public delegate IPortal PortalFinder(Scene scene, string id);

        /// <summary>
        /// True from the moment travel begins until the destination arrival sequence completes
        /// (including any cinematic walk-out). Auto-triggered portals (e.g. Entrance) check this
        /// to avoid re-firing when the player passes through the destination collider on arrival.
        /// </summary>
        public static bool IsTraveling { get; private set; }

        /// <summary>
        /// Starts the travel sequence from <paramref name="fromPortal"/> to its destination portal.
        /// </summary>
        public static void Travel(IPortal fromPortal, PortalFinder finder, PortalTravelHooks hooks = null) {
            if (fromPortal == null) {
                return;
            }

            if (finder == null) {
                throw new ArgumentNullException(nameof(finder));
            }

            if (IsTraveling) {
                return;
            }

            IsTraveling = true;

            // Disable hero controls + damage up-front. Doors call BeginTravelTransition before
            // their open animation, but Entrances trigger automatically and rely on this safety call.
            SetHeroTransitionState(false);

            // Host the master coroutine on ScreenService — it's DontDestroyOnLoad so it survives
            // the scene unload that happens mid-sequence.
            G.Screen.StartCoroutine(TravelRoutine(fromPortal, finder, hooks ?? new PortalTravelHooks()));
        }

        // Shared mutable context: PostLoad runs inside SceneTravelService and needs to write
        // the resolved destination portal back to the outer TravelRoutine so the AfterArrival
        // hook can target it.
        private sealed class TravelContext {
            public IPortal FromPortal;
            public PortalFinder Finder;
            public PortalTravelHooks Hooks;
            public IPortal TargetPortal;
        }

        private static IEnumerator TravelRoutine(IPortal fromPortal, PortalFinder finder, PortalTravelHooks hooks) {
            var ctx = new TravelContext { FromPortal = fromPortal, Finder = finder, Hooks = hooks };

            try {
                if (hooks.BeforeFadeOut != null) {
                    yield return hooks.BeforeFadeOut();
                }

                yield return G.Screen.FadeOutCoroutine(FadeDuration);

                var targetRef = fromPortal.TargetScene;
                var currentScene = fromPortal.gameObject.scene;
                var sameScene = IsSameScene(currentScene, targetRef);

                if (sameScene) {
                    yield return ArriveCoroutine(currentScene, ctx);
                } else {
                    // PostLoad runs after the scene loads and BEFORE AfterTransition fires
                    // (where scene-state restoration happens). Putting teleport + notify there
                    // preserves the original ordering.
                    yield return G.SceneTravel.LoadScene(targetRef, new SceneLoadOptions {
                        PostLoad = scene => ArriveCoroutine(scene, ctx),
                    });
                }

                // Fade in. If an arrival hook exists, fire fade-in concurrently and gate control
                // restoration on the hook so the destination's collider stays "in travel" during
                // the walk-out.
                if (hooks.AfterArrival != null) {
                    G.Screen.FadeIn(FadeDuration);
                    yield return hooks.AfterArrival(ctx.TargetPortal);
                } else {
                    yield return G.Screen.FadeInCoroutine(FadeDuration);
                }
            } finally {
                SetHeroTransitionState(true);
                IsTraveling = false;
            }
        }

        private static IEnumerator ArriveCoroutine(Scene targetScene, TravelContext ctx) {
            ctx.TargetPortal = ctx.Finder(targetScene, ctx.FromPortal.TargetId);

            var spawnPos = ctx.Hooks.ResolveSpawnPosition != null && ctx.TargetPortal != null
                ? ctx.Hooks.ResolveSpawnPosition(ctx.TargetPortal)
                : ctx.TargetPortal?.GetEntryPosition() ?? Vector3.zero;

            TeleportPlayerTo(ctx.TargetPortal, spawnPos);

            // Keep the screen faded while Cinemachine applies teleport warp in the loaded scene.
            yield return WaitForCameraSnapAfterTeleport();

            ctx.TargetPortal?.NotifyEntered();
            ctx.FromPortal.NotifyEntered();
        }

        private static bool IsSameScene(Scene currentScene, SceneReference targetRef) {
            // Prefer GUID comparison (rename-safe). Fall back to name comparison if the catalog is
            // not configured or the current scene is missing from it.
            var catalog = G.SceneCatalog;
            if (catalog != null && !string.IsNullOrEmpty(targetRef.SceneGuid)) {
                var currentGuid = catalog.GuidForScene(currentScene);
                if (!string.IsNullOrEmpty(currentGuid)) {
                    return string.Equals(currentGuid, targetRef.SceneGuid, StringComparison.Ordinal);
                }
            }

            return string.Equals(currentScene.name, targetRef.GetSceneName(), StringComparison.Ordinal);
        }

        private static void TeleportPlayerTo(IPortal targetPortal, Vector3 targetPosition) {
            if (targetPortal == null) {
                Debug.LogWarning("Target portal not found during travel.");
                return;
            }

            var playerController = G.Hero.Controller;
            if (playerController == null) {
                // Fallback for cases where the hero service has not registered yet.
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) {
                    playerController = player.GetComponent<PlayerController>();
                }
            }

            if (playerController == null) {
                Debug.LogWarning("PlayerController not found during portal travel.");
                return;
            }

            var previousPosition = playerController.transform.position;
            playerController.TeleportTo(targetPosition);
            G.Camera?.NotifyTargetTeleported(playerController.transform, targetPosition - previousPosition);
        }

        private static IEnumerator WaitForCameraSnapAfterTeleport() {
            if (G.Camera == null) {
                yield break;
            }

            for (int i = 0; i < MaxCameraSnapWaitFrames; i++) {
                if (G.Camera.TryApplyPendingTeleports()) {
                    yield break;
                }

                yield return null;
            }
        }

        private static void SetHeroTransitionState(bool isEnabled) {
            var playerController = G.Hero.Controller;
            if (playerController == null) {
                return;
            }

            playerController.SetCanTakeDamage(isEnabled);
            playerController.SetControlsEnabled(isEnabled);
        }
    }
}
