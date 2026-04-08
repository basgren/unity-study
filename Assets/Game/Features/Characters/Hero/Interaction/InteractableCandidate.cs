using Game.Core.Components.Interaction;
using UnityEngine;
using UnityEngine.Localization;

namespace Game.Features.Characters.Hero.Interaction {
    /// <summary>
    /// Adapter that exposes an <see cref="InteractableBase"/> as an
    /// <see cref="IInteractionCandidate"/>. Pooled by <see cref="TriggerInteractionProvider"/>
    /// so the resolver does not pay a per-frame allocation per in-range interactable —
    /// the provider holds one instance per tracked target and refreshes its state via
    /// <see cref="Refresh"/> each tick.
    /// </summary>
    internal class InteractableCandidate : IInteractionCandidate {
        private InteractableBase interactable;
        private float sqrDistance;

        public void Refresh(InteractableBase interactable, Vector3 referencePoint) {
            this.interactable = interactable;
            sqrDistance = (interactable.transform.position - referencePoint).sqrMagnitude;
        }

        public int Priority => interactable.InteractionPriority;
        public LocalizedString ActionText => interactable.ActionText;
        public float SqrDistanceFromGrabPoint => sqrDistance;
        public bool IsValid => interactable != null && interactable.CanInteract();
        public int StableId => interactable != null ? interactable.GetInstanceID() : 0;

        public void OnHoverEnter() {
            if (interactable != null) {
                interactable.SetHighlighted(true);
            }
        }

        public void OnHoverExit() {
            if (interactable != null) {
                interactable.SetHighlighted(false);
            }
        }

        public IInteractionHandle Execute() {
            if (interactable != null) {
                interactable.Interact();
            }
            return null;
        }
    }
}
