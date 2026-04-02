using System.Collections.Generic;
using Game.Core.Bootstrap;
using UnityEngine;

namespace Game.Core.Models.Dialog {
    /// <summary>
    /// Loads dialog definitions from locale-specific JSON files under Resources/Locale/.
    /// Caches loaded dialogs until <see cref="ClearCache"/> is called (e.g., on locale switch).
    /// </summary>
    public static class DialogLoader {
        private static readonly Dictionary<string, DialogDef> Cache = new();

        private static string GetPath(string locale, string dialogId) {
            return $"Locale/{locale}/Dialogs/{dialogId}.{locale}";
        }
        
        public static DialogDef Load(string dialogId) {
            if (Cache.TryGetValue(dialogId, out var cached)) {
                return cached;
            }

            var locale = G.Locale.CurrentLocale;
            var path = GetPath(locale, dialogId);
            var textAsset = Resources.Load<TextAsset>(path);

            if (textAsset == null) {
                Debug.LogError($"DialogLoader: dialog not found at 'Resources/{path}'.");
                return null;
            }

            var def = DialogParser.Parse(textAsset.text);
            Cache[dialogId] = def;
            return def;
        }

        /// <summary>
        /// Clears all cached dialog definitions. Called by LocaleService when locale changes.
        /// </summary>
        public static void ClearCache() {
            Cache.Clear();
        }
    }
}
