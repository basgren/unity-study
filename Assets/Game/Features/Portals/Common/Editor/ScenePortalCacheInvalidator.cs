#if UNITY_EDITOR
using System;

namespace Game.Features.Portals.Common.Editor {
    /// <summary>
    /// Lets the shared SceneReferenceDrawer flush per-portal-kind editor caches
    /// without referencing each cache type directly. Each ScenePortalCache subscribes
    /// its own InvalidateAll on load.
    /// </summary>
    public static class ScenePortalCacheInvalidator {
        public static event Action OnInvalidate;

        public static void InvalidateAll() {
            OnInvalidate?.Invoke();
        }
    }
}
#endif
