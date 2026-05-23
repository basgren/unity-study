namespace Game.Features.Characters.Hero.Interaction {
    /// <summary>
    /// Returned from <see cref="IInteractionCandidate.Execute"/> when an interaction
    /// takes ongoing ownership of the Interact input across multiple frames (e.g.
    /// barrel dragging). While <see cref="IsActive"/> is true, the resolver suspends
    /// candidate gathering, ranking, and hover updates.
    /// </summary>
    public interface IInteractionHandle {
        /// <summary>
        /// True while the interaction is still in progress. The producer (e.g.
        /// <c>DragAbility</c>) flips this to false when the action ends, regardless
        /// of why (release, jump, lost ground, ...).
        /// </summary>
        bool IsActive { get; }
    }
}
