using System;
using Game.Core.Bootstrap;
using Game.Core.Utils;
using UnityEngine;

namespace Game.Core.Services.SceneState {
    /// <summary>
    /// Marker component that gives a scene object a stable ID and a save tier.
    /// Sibling components implement IStateSaver to contribute individual slots of state.
    /// One StateRoot per saveable GameObject — enforced by DisallowMultipleComponent.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StateRoot : MonoBehaviour {
        [SerializeField, HideInInspector]
        private string saveId;

        /// <summary>
        /// When true, this object is excluded from capture and restore.
        /// Use this for dynamically spawned objects (e.g. dropped coins) that share a prefab
        /// with scene-placed saveable instances.
        /// </summary>
        [SerializeField]
        private bool skipSave;

        [SerializeField]
        private SaveTier tier = SaveTier.Persistent;

        /// <summary>Stable string ID unique within the scene, auto-generated on scene save.</summary>
        public string SaveId => saveId;

        /// <summary>
        /// When true, this object is excluded from capture and restore.
        /// Use this for dynamically spawned objects (e.g. dropped coins) that share a prefab
        /// with scene-placed saveable instances.
        /// </summary>
        public bool SkipSave => skipSave;

        /// <summary>Controls how long this object's state is retained.</summary>
        public SaveTier Tier => tier;

        private IStateSaver[] savers;

        private void Awake() {
            savers = GetComponents<IStateSaver>();
        }

        private void Start() {
            // Pull saved state after all Awake calls have completed so sibling components are ready.
            if (G.SceneState != null) {
                G.SceneState.RestoreInto(this, savers);
            }
        }

        /// <summary>Writes all saver slots into the given blob.</summary>
        internal void CaptureInto(StateBlob blob) {
            foreach (var s in savers) {
                s.Capture(blob.Writer(s.Slot));
            }
        }

        /// <summary>Reads all matching slots from the given blob and applies them to the savers.</summary>
        internal void ApplyFrom(StateBlob blob) {
            foreach (var s in savers) {
                if (blob.TryReader(s.Slot, out var r)) {
                    s.Restore(r);
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate() {
            if (Application.isPlaying) {
                return;
            }

            // Prefab assets must not carry an ID — each scene instance gets its own via
            // StateRootIdAssigner (runs on scene save).
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this)) {
                if (!string.IsNullOrEmpty(saveId)) {
                    saveId = string.Empty;
                    UnityEditor.EditorUtility.SetDirty(this);
                }
            }
        }
#endif
    }
}
