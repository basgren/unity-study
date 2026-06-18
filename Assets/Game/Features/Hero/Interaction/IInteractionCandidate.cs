using UnityEngine;
using UnityEngine.Localization;

namespace Game.Features.Characters.Hero.Interaction {
    /// <summary>
    /// A single, ranked interaction option offered to the resolver by an
    /// <see cref="IInteractionProvider"/>. The resolver picks the best one based on
    /// <see cref="Priority"/> and <see cref="SqrDistanceFromGrabPoint"/>, drives hover
    /// state through <see cref="OnHoverEnter"/> / <see cref="OnHoverExit"/>, and runs
    /// <see cref="Execute"/> when the player presses the Interact button.
    /// </summary>
    public interface IInteractionCandidate {
        /// <summary>
        /// Higher priority wins. Default world interactables use 0; the barrel
        /// candidate uses a higher value to win overlap conflicts.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Localized verb shown by the HUD hint widget. May be null or empty —
        /// in that case the candidate still participates in selection but the hint
        /// is hidden over it.
        /// </summary>
        LocalizedString ActionText { get; }

        /// <summary>
        /// Squared distance to the hero's GrabPoint. Squared to avoid sqrt during
        /// ranking. All providers must compute distance against the same canonical
        /// reference point exposed by the resolver.
        /// </summary>
        float SqrDistanceFromGrabPoint { get; }

        /// <summary>
        /// Contextual gate. Returning false hides the candidate from selection this
        /// frame without removing it from the underlying provider's tracking.
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// Stable identity used by the resolver to detect "same target" across frames
        /// even when the candidate object itself is rebuilt. Typically the underlying
        /// component's entity id.
        /// </summary>
        EntityId StableId { get; }

        /// <summary>Called by the resolver when this candidate becomes the selected one.</summary>
        void OnHoverEnter();

        /// <summary>Called by the resolver when this candidate stops being the selected one.</summary>
        void OnHoverExit();

        /// <summary>
        /// Runs the interaction. Return null for one-shot interactions (Bonfire, Switch,
        /// InfoSign, DialogNPC). Return a handle when the interaction takes ongoing
        /// ownership of the input (e.g. barrel dragging) — the resolver suspends all
        /// candidate evaluation while the handle reports active.
        /// </summary>
        IInteractionHandle Execute();
    }
}