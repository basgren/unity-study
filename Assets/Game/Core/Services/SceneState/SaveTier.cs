namespace Game.Core.Services.SceneState {
    /// <summary>
    /// Controls how long a piece of saved state persists.
    /// </summary>
    public enum SaveTier {
        /// <summary>Cleared when the player rests at a bonfire. Used for transient state such as moved barrels.</summary>
        Session,

        /// <summary>Persists for the entire playthrough. Used for permanently opened doors or consumed items.</summary>
        Persistent,
    }
}
