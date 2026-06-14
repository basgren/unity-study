using System;
using System.Collections;
using Game.Core.Bootstrap;
using UnityEngine;

namespace Game.Core.Services.Scene {
    /// <summary>
    /// Shared menu-to-scene transition: fades to black, closes all open menus and loads the
    /// target scene while fully faded, then fades back in. Used by the main menu (Continue,
    /// New Game) and the pause menu (Exit to Menu) so they all share one transition feel.
    /// </summary>
    public static class SceneTransition {
        /// <summary>
        /// Runs the faded scene transition. Fade durations come from <see cref="Configs.MainConfig"/>.
        /// The work runs as a coroutine on the DontDestroyOnLoad <see cref="ScreenService"/>, so it
        /// survives the calling menu being destroyed by CloseAll — pass the scene name and any state
        /// changes via <paramref name="whileFaded"/> rather than relying on the caller staying alive.
        /// </summary>
        /// <param name="sceneName">Scene to load once the screen is fully faded out.</param>
        /// <param name="options">Scene load options (e.g. SkipStateCapture).</param>
        /// <param name="whileFaded">Optional work to run while black, before menus close and the scene loads.</param>
        public static Coroutine FadeToScene(
            string sceneName,
            SceneLoadOptions options = default,
            Action whileFaded = null
        ) {
            var config = G.Config;
            return G.Screen.RunWhenFadeOut(
                config.sceneFadeOutTime,
                config.sceneFadeInTime,
                () => Run(sceneName, options, whileFaded)
            );
        }

        private static IEnumerator Run(string sceneName, SceneLoadOptions options, Action whileFaded) {
            whileFaded?.Invoke();

            var menusClosed = false;
            G.Menu.CloseAll(() => menusClosed = true);
            while (!menusClosed) {
                yield return null;
            }

            yield return G.SceneTravel.LoadScene(sceneName, options);
        }
    }
}
