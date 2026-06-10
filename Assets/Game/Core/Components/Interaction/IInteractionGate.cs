namespace Game.Core.Components.Interaction {
    /// <summary>
    /// Outcome of an <see cref="IInteractionGate"/> consulted when the player activates an interactable.
    /// </summary>
    public enum InteractionGateResult {
        /// <summary>The interaction may run now.</summary>
        Allow,

        /// <summary>The interaction is blocked. The gate has already played its own rejected feedback.</summary>
        Reject,

        /// <summary>
        /// The gate has taken over activation asynchronously (e.g. an unlock sequence) and the interaction
        /// must not run now. The gate is responsible for calling <see cref="InteractableBase.Activate"/>
        /// itself once its sequence completes.
        /// </summary>
        Deferred,
    }

    /// <summary>
    /// Optional gate placed on the same GameObject as an <see cref="InteractableBase"/>. Consulted by
    /// <see cref="InteractableBase.Interact"/> before the interaction runs: it can allow, reject (with its
    /// own feedback, e.g. a locked sound), or defer the interaction and drive it asynchronously. The object
    /// stays a normal hover/hint target, so the player can always attempt the interaction and get feedback.
    /// </summary>
    public interface IInteractionGate {
        /// <summary>
        /// Called once when the player activates the interactable, before the interaction runs. May start
        /// async work and/or play feedback. Returns how the interaction should proceed.
        /// </summary>
        InteractionGateResult OnInteractRequested();
    }
}
