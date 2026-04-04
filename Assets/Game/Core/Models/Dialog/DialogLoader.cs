using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.Models.Dialog {
    /// <summary>
    /// Loads locale-agnostic dialog graph definitions from Resources/Dialogs/.
    /// Text is stored as localization keys and resolved at runtime by DialogService.
    /// </summary>
    public static class DialogLoader {
        private static readonly Dictionary<string, DialogDef> Cache = new();

        public static DialogDef Load(string dialogId) {
            if (Cache.TryGetValue(dialogId, out var cached)) {
                return cached;
            }

            var path = $"Dialogs/{dialogId}";
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
