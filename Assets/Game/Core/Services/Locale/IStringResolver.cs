namespace Game.Core.Services.Locale {
    /// <summary>
    /// Resolves a localization key to its translated string for the active locale.
    /// </summary>
    public interface IStringResolver {
        string Resolve(string key);
    }
}