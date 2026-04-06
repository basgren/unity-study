using System;
using Game.Core.Bootstrap;
using Game.Core.Components.Animation;
using Game.Core.Components.Interaction;
using Game.Core.Services;
using Game.Core.Services.Tween.Components;
using Game.Core.Utils;
using Game.Features.Doors;
using UnityEditor;
using UnityEngine;

namespace Game.Features.Interactive.Bonfire {
    public enum BonfireState {
        Undiscovered,
        Discovered,
        Current,
    }

    [RequireComponent(typeof(MultiStateSpriteAnimator))]
    public class Bonfire : InteractableBase {
        [SerializeField]
        private string checkpointId;

        [SerializeField]
        private Transform spawnPoint;

        [SerializeField]
        private GameObject smokeEffect;

        private MultiStateSpriteAnimator animator;
        private BonfireState bonfireState;
        private TweenGroup tweenGroup;
        private SpriteRenderer spriteRenderer;

        /// <summary>
        /// Local checkpoint ID, unique within this scene.
        /// </summary>
        public string CheckpointId => checkpointId;

        public Vector2 GetSpawnPosition() {
            return spawnPoint != null ? spawnPoint.position : (Vector2)transform.position;
        }

        protected override void Awake() {
            base.Awake();
            animator = GetComponent<MultiStateSpriteAnimator>();
            // TODO: [BG] Move it ot smokeEffect component. In generat a common component may be build which
            //   will use tweens to show/hide elements and optionally disable them when they are hidden.
            tweenGroup = smokeEffect.GetComponent<TweenGroup>();
            spriteRenderer = smokeEffect.GetComponent<SpriteRenderer>();
        }

        private void Start() {
            var state = G.Checkpoint.GetBonfireState(gameObject.scene.name, checkpointId);
            SetState(state, true);
            G.Checkpoint.OnCheckpointChanged += OnCheckpointChanged;
        }

        private void OnDestroy() {
            if (G.Checkpoint != null) {
                G.Checkpoint.OnCheckpointChanged -= OnCheckpointChanged;
            }
        }

        protected override void DoInteract() {
            if (bonfireState == BonfireState.Current) {
                return;
            }

            var checkpointRef = new CheckpointRef {
                Scene = SceneReference.FromScene(gameObject.scene),
                LocalId = checkpointId,
            };

            G.Checkpoint.Activate(checkpointRef);
        }

        private void OnCheckpointChanged(CheckpointRef? data) {
            var newState = G.Checkpoint.GetBonfireState(gameObject.scene.name, checkpointId);
            SetState(newState);
        }

        private void SetState(BonfireState state, bool immediateTransition = false) {
            bonfireState = state;
            UpdateView(immediateTransition);
        }

        private void UpdateView(bool immediateTransition = false) {
            var hasSmoke = false;
            string clip;

            switch (bonfireState) {
                case BonfireState.Current:
                    clip = "current";
                    hasSmoke = true;
                    break;

                case BonfireState.Discovered:
                    clip = "discovered";
                    break;

                default:
                    clip = "undiscovered";
                    break;
            }

            if (animator != null) {
                animator.SetClip(clip);
            }

            UpdateSmokeState(hasSmoke, immediateTransition);
        }

        private void UpdateSmokeState(bool hasSmoke, bool isImmediate = false) {
            if (isImmediate) {
                spriteRenderer.color = hasSmoke ? Color.white : Color.clear;
                smokeEffect.SetActive(hasSmoke);
                return;
            }

            if (smokeEffect.activeSelf != hasSmoke) {
                var tween = tweenGroup.Get(hasSmoke ? "show" : "hide");

                if (hasSmoke) {
                    spriteRenderer.color = Color.clear;
                    smokeEffect.SetActive(true);

                    tween.Play((eased, _) => {
                        // Extra protection, as player may leave scene while tween is in progress.
                        if (spriteRenderer != null) {
                            spriteRenderer.color = Color.Lerp(Color.clear, Color.white, eased);
                        }
                    });
                } else {
                    tween.Play((eased, _) => {
                        // Extra protection, as player may leave scene while tween is in progress.
                        if (spriteRenderer != null) {
                            spriteRenderer.color = Color.Lerp(Color.white, Color.clear, eased);
                        }
                    }, () => {
                        smokeEffect.SetActive(false);
                    });
                }
            }
        }

#if UNITY_EDITOR
        private const int DefaultGeneratedLength = 5;

        private void OnValidate() {
            if (Application.isPlaying) {
                return;
            }

            // If this component is on a prefab asset, keep checkpointId empty so it does NOT
            // propagate to instances.
            if (PrefabUtility.IsPartOfPrefabAsset(this)) {
                if (!string.IsNullOrEmpty(checkpointId)) {
                    checkpointId = string.Empty;
                    EditorUtility.SetDirty(this);
                }

                return;
            }

            // If this is a prefab instance and it still matches the prefab's stored id,
            // generate a unique one for this instance.
            var source = PrefabUtility.GetCorrespondingObjectFromSource(this) as Bonfire;
            var isInheritedFromPrefab =
                source != null && string.Equals(checkpointId, source.checkpointId, StringComparison.Ordinal);

            if (isInheritedFromPrefab) {
                checkpointId = $"Bonfire_{IdUtils.GenerateId(DefaultGeneratedLength)}";
                EditorUtility.SetDirty(this);
                return;
            }

            if (string.IsNullOrWhiteSpace(checkpointId)) {
                Debug.LogWarning($"Bonfire '{name}' has no checkpointId assigned.", this);
            }
        }
#endif
    }
}
