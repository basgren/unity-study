using System.Collections.Generic;

namespace Game.Features.Characters.Hero.Interaction {
    /// <summary>
    /// A source of interaction candidates. The resolver discovers all providers on the
    /// hero via <c>GetComponents</c> and asks each one to contribute candidates each frame.
    /// Providers should write into the supplied buffer rather than allocating their own.
    /// </summary>
    public interface IInteractionProvider {
        /// <summary>
        /// Append zero or more candidates to <paramref name="output"/>.
        /// Implementations should not clear the buffer; the resolver owns it.
        /// </summary>
        void CollectCandidates(List<IInteractionCandidate> output);
    }
}
