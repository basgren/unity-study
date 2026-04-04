using System;
using Game.Core.Bootstrap;
using Game.Core.Models.Dialog;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace Game.Core.Services.Locale {
    /// <summary>
    /// Thin wrapper around Unity Localization. Persists the chosen language in
    /// <see cref="Configs.GameSettings"/> and notifies listeners on change.
    /// </summary>
    public sealed class LocaleService : MonoBehaviour {
        private const string DefaultLocale = "en";

        public string CurrentLocale {
            get {
                var locale = LocalizationSettings.SelectedLocale;
                return locale != null ? locale.Identifier.Code : DefaultLocale;
            }
        }

        public event Action<string> OnLocaleChanged;

        public void Init() {
            var op = LocalizationSettings.InitializationOperation;
            if (!op.IsDone) {
                op.WaitForCompletion();
            }

            LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;

            var saved = G.Settings.Current.Locale ?? DefaultLocale;
            SetLocale(saved);
        }

        public void SetLocale(string locale) {
            var locales = LocalizationSettings.AvailableLocales.Locales;
            for (int i = 0; i < locales.Count; i++) {
                if (locales[i].Identifier.Code == locale) {
                    LocalizationSettings.SelectedLocale = locales[i];
                    return;
                }
            }

            Debug.LogWarning($"LocaleService: locale '{locale}' not found in LocalizationSettings.");
        }

        private void HandleLocaleChanged(UnityEngine.Localization.Locale locale) {
            var code = locale.Identifier.Code;
            G.Settings.Current.Locale = code;
            G.Settings.Save();
            DialogLoader.ClearCache();
            OnLocaleChanged?.Invoke(code);
        }

        private void OnDestroy() {
            LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
        }
    }
}
