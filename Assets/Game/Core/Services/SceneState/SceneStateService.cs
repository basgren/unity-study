using System;
using System.Collections.Generic;
using Game.Core.Bootstrap;
using UnityEngine;

namespace Game.Core.Services.SceneState {
    /// <summary>
    /// Global service that captures and restores the state of scene objects across scene transitions
    /// and bonfire rests.
    ///
    /// State is keyed by (sceneGuid, saveId) and stored in two tiers:
    ///   Session   — cleared on bonfire rest; survives scene reloads within one session.
    ///   Persistent — kept until the end of the playthrough.
    ///
    /// Capture happens automatically in BeforeUnload (fired by SceneTravelService).
    /// Restore happens in StateRoot.Start(), which pulls its own blob after Awake completes.
    /// </summary>
    public class SceneStateService : MonoBehaviour {
        // sceneGuid → saveId → StateBlob
        private readonly Dictionary<string, Dictionary<string, StateBlob>> sessionStore = new();
        private readonly Dictionary<string, Dictionary<string, StateBlob>> persistentStore = new();

        private SceneCatalog catalog;
        private int restoreDepth;

        /// <summary>
        /// True while saved state is being applied to live scene objects.
        /// Runtime side effects should be suppressed during this phase.
        /// </summary>
        public bool IsRestoring => restoreDepth > 0;

        /// <summary>
        /// Must be called from GInit after both G.Config and G.SceneTravel are set up.
        /// </summary>
        public void Init() {
            catalog = G.Config.SceneCatalog;

            if (catalog == null) {
                Debug.LogWarning(
                    "[SceneStateService] SceneCatalog is not assigned in MainConfig. " +
                    "State capture/restore will be disabled until it is set up."
                );
                return;
            }

            G.SceneTravel.BeforeUnload += OnBeforeUnload;
        }

        private void OnDestroy() {
            if (G.SceneTravel != null) {
                G.SceneTravel.BeforeUnload -= OnBeforeUnload;
            }
        }

        private void OnBeforeUnload(UnityEngine.SceneManagement.Scene scene) {
            CaptureScene(scene);
        }

        // ---- Public API ----

        /// <summary>Captures the current state of every StateRoot in the given scene.</summary>
        public void CaptureScene(UnityEngine.SceneManagement.Scene scene) {
            if (catalog == null) {
                return;
            }

            var sceneId = catalog.GuidForScene(scene);
            if (string.IsNullOrEmpty(sceneId)) {
                return;
            }

            foreach (var root in FindStateRootsInScene(scene)) {
                if (root.SkipSave) {
                    continue;
                }

                if (string.IsNullOrEmpty(root.SaveId)) {
                    Debug.LogWarning($"[SceneStateService] StateRoot on '{root.name}' has no saveId — skipped.", root);
                    continue;
                }

                var blob = GetOrCreateBlob(StoreFor(root.Tier), sceneId, root.SaveId);
                root.CaptureInto(blob);
            }
        }

        /// <summary>Immediately writes a single slot for the given StateRoot into the store.</summary>
        public void PushSlot(StateRoot root, string slot, Action<IStateWriter> write) {
            if (catalog == null || root == null || root.SkipSave) {
                return;
            }

            var sceneId = catalog.GuidForScene(root.gameObject.scene);
            if (string.IsNullOrEmpty(sceneId)) {
                return;
            }

            var blob = GetOrCreateBlob(StoreFor(root.Tier), sceneId, root.SaveId);
            write(blob.Writer(slot));
        }

        /// <summary>Called when the player rests at a bonfire. Clears all session-tier state.</summary>
        public void OnBonfireRest() {
            sessionStore.Clear();
        }

        // ---- Internal (called by StateRoot) ----

        internal void RestoreInto(StateRoot root, IStateSaver[] savers) {
            if (catalog == null || root.SkipSave) {
                return;
            }

            var sceneId = catalog.GuidForScene(root.gameObject.scene);

            if (TryGetBlob(StoreFor(root.Tier), sceneId, root.SaveId, out var blob)) {
                restoreDepth++;

                try {
                    root.ApplyFrom(blob);
                } finally {
                    restoreDepth--;
                }
            }
        }

        // ---- Helpers ----

        private Dictionary<string, Dictionary<string, StateBlob>> StoreFor(SaveTier tier) {
            return tier == SaveTier.Persistent ? persistentStore : sessionStore;
        }

        private static StateBlob GetOrCreateBlob(
            Dictionary<string, Dictionary<string, StateBlob>> store,
            string sceneId,
            string saveId
        ) {
            if (!store.TryGetValue(sceneId, out var byId)) {
                byId = new Dictionary<string, StateBlob>();
                store[sceneId] = byId;
            }

            if (!byId.TryGetValue(saveId, out var blob)) {
                blob = new StateBlob();
                byId[saveId] = blob;
            }

            return blob;
        }

        private static bool TryGetBlob(
            Dictionary<string, Dictionary<string, StateBlob>> store,
            string sceneId,
            string saveId,
            out StateBlob result
        ) {
            if (!string.IsNullOrEmpty(sceneId) &&
                store.TryGetValue(sceneId, out var byId) &&
                byId.TryGetValue(saveId, out result)) {
                return true;
            }

            result = null;
            return false;
        }

        private static List<StateRoot> FindStateRootsInScene(UnityEngine.SceneManagement.Scene scene) {
            var result = new List<StateRoot>();
            foreach (var go in scene.GetRootGameObjects()) {
                result.AddRange(go.GetComponentsInChildren<StateRoot>(true));
            }

            return result;
        }
    }
}
