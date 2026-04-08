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
        // Pooling avoids per-frame GC pressure from rebuilding adapters in Update.
        private readonly List<InteractableBase> inRange = new();
        private readonly List<InteractableCandidate> cachedCandidates = new();
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
                    inRange.RemoveAt(i);
                    cachedCandidates.RemoveAt(i);
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

            if (!inRange.Contains(interactable)) {
                inRange.Add(interactable);
                cachedCandidates.Add(new InteractableCandidate());
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

            inRange.RemoveAt(index);
            cachedCandidates.RemoveAt(index);

            // The resolver owns hover state, so we deliberately do NOT clear hover
            // here. The resolver will notice the candidate disappeared on its next
            // tick and call OnHoverExit through the candidate adapter.
        }
    }
}
