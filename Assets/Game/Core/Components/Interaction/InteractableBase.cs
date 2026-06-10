using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;

namespace Game.Core.Components.Interaction {
    /// <summary>
    /// Base for trigger-based world interactables (Bonfire, Switch, InfoSign, ...).
    /// Holds the metadata used by the player interaction resolver: priority,
    /// localized action verb, and a contextual gate. The resolver owns hover state
    /// and calls <see cref="SetHighlighted"/> when this object becomes or stops being
    /// the player's selected target.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public abstract class InteractableBase : MonoBehaviour {
        /// <summary>
        /// Collider used to detect interaction. This may be separate from the
        /// collider used for collision detection. This collider is forced to be a trigger.
        /// </summary>
        [Header("Interactable")]
        [SerializeField]
        private Collider2D interactionCollider;

        /// <summary>
        /// Higher value wins overlap conflicts. Default 0; raise on prefabs that should
        /// take precedence over neighbours of the same kind. The barrel candidate uses 100.
        /// </summary>
        [SerializeField]
        private int interactionPriority;

        /// <summary>
        /// Localized verb shown by the HUD interaction hint when this object is the
        /// player's selected target. Leave empty to hide the hint without removing the
        /// object from selection (e.g. DialogNPC until its verb is agreed).
        /// </summary>
        [SerializeField]
        private LocalizedString actionText;

        [SerializeField]
        private UnityEvent onInteract;

        public int InteractionPriority => interactionPriority;
        public LocalizedString ActionText => actionText;

        // Optional gates on this object that can block activation at interact time (e.g. a key lock).
        // Cached in Awake; empty for the common case so gateless interactables pay nothing extra.
        private IInteractionGate[] interactionGates;

        public void Interact() {
            if (!PassesGates()) {
                return;
            }

            Activate();
        }

        /// <summary>
        /// Runs the actual interaction (the <c>onInteract</c> event and <see cref="DoInteract"/>), bypassing
        /// gates. Called by <see cref="Interact"/> when gates allow, or by a gate after a deferred sequence
        /// completes (e.g. a key lock that opens the door once its unlock animation/SFX has played).
        /// </summary>
        public void Activate() {
            onInteract?.Invoke();
            DoInteract();
        }

        /// <summary>
        /// Consults optional <see cref="IInteractionGate"/> components on this object. Returns false when any
        /// gate rejects or defers the interaction (a deferring gate runs activation itself, asynchronously).
        /// </summary>
        private bool PassesGates() {
            if (interactionGates == null) {
                return true;
            }

            foreach (var gate in interactionGates) {
                if (gate.OnInteractRequested() != InteractionGateResult.Allow) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Contextual gate consulted by the resolver each frame. Override in subclasses
        /// that can become temporarily uninteractable (e.g. a single-use Switch after use).
        /// </summary>
        public virtual bool CanInteract() {
            return true;
        }

        /// <summary>
        /// Called by the resolver when this interactable becomes (or stops being) the
        /// player's selected target. Subclasses override <see cref="OnHoveredChange"/>
        /// to update their visuals.
        /// </summary>
        public void SetHighlighted(bool value) {
            OnHoveredChange(value);
        }

        /// <summary>
        /// Override in descendant classes to perform the interaction.
        /// </summary>
        protected virtual void DoInteract() {
            // Do nothing. Override in descendant classes to perform some action.
        }

        protected virtual void Awake() {
            interactionCollider.isTrigger = true;
            interactionGates = GetComponents<IInteractionGate>();
        }

        /// <summary>
        /// Override to react to hover state changes (sprite color, outline, etc).
        /// Called only via <see cref="SetHighlighted"/>, which is in turn called only
        /// by the player interaction resolver.
        /// </summary>
        protected virtual void OnHoveredChange(bool isHovered) {
        }
    }
}
