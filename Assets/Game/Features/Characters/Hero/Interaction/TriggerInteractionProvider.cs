using System.Collections.Generic;
using Game.Core.Components.Interaction;
using UnityEngine;

namespace Game.Features.Characters.Hero.Interaction {
    /// <summary>
    /// Tracks <see cref="InteractableBase"/> instances currently inside the hero's
    /// interaction trigger and exposes them as candidates to the resolver. Replaces
    /// the inline trigger handling that used to live in <c>PlayerController</c>.
    ///
    /// This component must sit on the same GameObject as the hero's BoxCollider2D
    /// so it receives <c>OnTriggerEnter2D</c> / <c>OnTriggerExit2D</c> callbacks.
    /// </summary>
    [RequireComponent(typeof(PlayerInteractionResolver))]
    public class TriggerInteractionProvider : MonoBehaviour, IInteractionProvider {
        // Parallel lists: cachedCandidates[i] is the pooled adapter for inRange[i].
        // overlapCounts[i] tracks how many of our child trigger colliders currently
        // overlap inRange[i]. Trigger callbacks bubble up from every child collider
        // to this Rigidbody2D, so the same interactable can fire enter/exit from the
        // body collider AND from short-lived attack hit areas. Ref-counting prevents
        // a transient sword swing from evicting an interactable the body still overlaps.
        // Pooling avoids per-frame GC pressure from rebuilding adapters in Update.
        private readonly List<InteractableBase> inRange = new();
        private readonly List<InteractableCandidate> cachedCandidates = new();
        private readonly List<int> overlapCounts = new();
        private PlayerInteractionResolver resolver;

        private void Awake() {
            resolver = GetComponent<PlayerInteractionResolver>();
        }

        public void CollectCandidates(List<IInteractionCandidate> output) {
            Vector3 origin = resolver.GrabPoint != null
                ? resolver.GrabPoint.position
                : transform.position;

            for (int i = inRange.Count - 1; i >= 0; i--) {
                var interactable = inRange[i];

                // Defensive: if the underlying object was destroyed without firing
                // OnTriggerExit2D (e.g. scene unload mid-frame), drop it now.
                if (interactable == null) {
                    RemoveAt(i);
                    continue;
                }

                var candidate = cachedCandidates[i];
                candidate.Refresh(interactable, origin);
                output.Add(candidate);
            }
        }

        private void OnTriggerEnter2D(Collider2D other) {
            if (!other.TryGetComponent<InteractableBase>(out var interactable)) {
                return;
            }

            int index = inRange.IndexOf(interactable);
            if (index < 0) {
                inRange.Add(interactable);
                cachedCandidates.Add(new InteractableCandidate());
                overlapCounts.Add(1);
            } else {
                overlapCounts[index]++;
            }
        }

        private void OnTriggerExit2D(Collider2D other) {
            if (!other.TryGetComponent<InteractableBase>(out var interactable)) {
                return;
            }

            int index = inRange.IndexOf(interactable);
            if (index < 0) {
                return;
            }

            overlapCounts[index]--;
            if (overlapCounts[index] > 0) {
                return;
            }

            RemoveAt(index);

            // The resolver owns hover state, so we deliberately do NOT clear hover
            // here. The resolver will notice the candidate disappeared on its next
            // tick and call OnHoverExit through the candidate adapter.
        }

        private void RemoveAt(int index) {
            inRange.RemoveAt(index);
            cachedCandidates.RemoveAt(index);
            overlapCounts.RemoveAt(index);
        }
    }
}
