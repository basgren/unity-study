using UnityEngine.Localization.Settings;

namespace Game.Core.Services.Locale {
    /// <summary>
    /// Resolves localization keys from a Unity Localization string table.
    /// Tables must be preloaded for synchronous access.
    /// </summary>
    public sealed class UnityStringResolver : IStringResolver {
        private readonly string tableName;

        public UnityStringResolver(string tableName) {
            this.tableName = tableName;
        }

        public string Resolve(string key) {
            if (string.IsNullOrEmpty(key)) {
                return "";
            }

            return LocalizationSettings.StringDatabase.GetLocalizedString(tableName, key);
        }
    }
}