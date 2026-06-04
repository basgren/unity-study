using System;
using System.Collections.Generic;
using Game.Features.Hero;
using UnityEngine;

namespace Game.Features.Characters.Hero.Interaction {
    /// <summary>
    /// Single source of truth for the hero's interaction selection.
    /// Gathers candidates from every <see cref="IInteractionProvider"/> on the hero
    /// each frame, ranks them by priority then squared distance from
    /// <see cref="GrabPoint"/>, drives hover state, and runs the selected candidate
    /// when the player presses Interact.
    ///
    /// While a candidate's <see cref="IInteractionCandidate.Execute"/> returns an
    /// active <see cref="IInteractionHandle"/> (e.g. barrel dragging), the resolver
    /// suspends gathering, ranking, and hover updates entirely. This guarantees
    /// modal interactions never compete with other candidates mid-action.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerInteractionResolver : MonoBehaviour {
        [Tooltip("Canonical reference point used by all providers to rank candidates by distance. " +
                 "Should match DragAbility.interactPoint (the GrabPoint child on the Hero prefab).")]
        [SerializeField]
        private Transform grabPoint;

        public Transform GrabPoint => grabPoint;
        public IInteractionCandidate CurrentCandidate { get; private set; }

        /// <summary>
        /// Fires whenever <see cref="CurrentCandidate"/> changes (including to/from null).
        /// HUD widgets subscribe to this rather than polling.
        /// </summary>
        public event Action<IInteractionCandidate> OnCurrentCandidateChanged;

        private PlayerController player;
        private readonly List<IInteractionProvider> providers = new();
        private readonly List<IInteractionCandidate> candidateBuffer = new();
        private IInteractionHandle activeHandle;

        private void Awake() {
            player = GetComponent<PlayerController>();
            GetComponents(providers);

            if (grabPoint == null) {
                Debug.LogWarning(
                    $"{nameof(PlayerInteractionResolver)}: GrabPoint is not assigned. " +
                    "Distance-based ranking will fall back to the hero root.",
                    this
                );
            }
        }

        private void Update() {
            // While a modal interaction owns the input, do nothing: no gathering,
            // no ranking, no hover updates, no Execute. The handle producer (e.g.
            // DragAbility) is responsible for ending its own action.
            if (activeHandle != null) {
                if (activeHandle.IsActive) {
                    return;
                }

                activeHandle = null;
            }

            if (!IsInputAvailable()) {
                SetCurrentCandidate(null);
                return;
            }

            var best = PickBestCandidate();
            SetCurrentCandidate(best);

            if (best != null && player.Actions.Interact.WasPressedThisFrame()) {
                var handle = best.Execute();
                if (handle != null && handle.IsActive) {
                    activeHandle = handle;
                    // Clear hover/current immediately — we're now in modal mode and
                    // should not show a hint until the action ends.
                    SetCurrentCandidate(null);
                }
            }
        }

        private bool IsInputAvailable() {
            // The Interact action lives on the Player action map. When the map is
            // disabled (menus, bonfire rest fade, dialog), we should drop selection
            // so the hint hides.
            return player.Actions.Interact.enabled;
        }

        private IInteractionCandidate PickBestCandidate() {
            candidateBuffer.Clear();

            for (int i = 0; i < providers.Count; i++) {
                providers[i].CollectCandidates(candidateBuffer);
            }

            IInteractionCandidate best = null;
            int bestPriority = int.MinValue;
            float bestSqrDist = float.PositiveInfinity;
            int bestStableId = int.MaxValue;

            for (int i = 0; i < candidateBuffer.Count; i++) {
                var candidate = candidateBuffer[i];
                if (candidate == null || !candidate.IsValid) {
                    continue;
                }

                int priority = candidate.Priority;
                float sqrDist = candidate.SqrDistanceFromGrabPoint;
                int stableId = candidate.StableId;

                if (priority > bestPriority
                    || (priority == bestPriority && sqrDist < bestSqrDist)
                    || (priority == bestPriority && sqrDist == bestSqrDist && stableId < bestStableId)
                ) {
                    best = candidate;
                    bestPriority = priority;
                    bestSqrDist = sqrDist;
                    bestStableId = stableId;
                }
            }

            return best;
        }

        private void SetCurrentCandidate(IInteractionCandidate next) {
            if (CurrentCandidate == null && next == null) {
                return;
            }

            // Compare by StableId rather than reference: providers may rebuild a
            // fresh adapter for the same underlying target each frame.
            if (CurrentCandidate != null
                && next != null
                && CurrentCandidate.StableId == next.StableId) {
                return;
            }

            if (CurrentCandidate != null) {
                CurrentCandidate.OnHoverExit();
            }

            CurrentCandidate = next;

            if (CurrentCandidate != null) {
                CurrentCandidate.OnHoverEnter();
            }

            OnCurrentCandidateChanged?.Invoke(CurrentCandidate);
        }

        private void OnDisable() {
            // Clear hover and selection when the hero is being torn down (scene change,
            // death, etc.). This prevents stale highlights on objects that survive.
            if (CurrentCandidate != null) {
                CurrentCandidate.OnHoverExit();
                CurrentCandidate = null;
                OnCurrentCandidateChanged?.Invoke(null);
            }
            activeHandle = null;
        }
    }
}
