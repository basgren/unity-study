using System;
using Game.Core.Bootstrap;
using Game.Core.Models.Dialog;
using UnityEngine;

namespace Game.Core.Services.Locale {
    /// <summary>
    /// Centralized locale management. Persists the chosen language in
    /// <see cref="Configs.GameSettings"/> and notifies listeners on change.
    /// </summary>
    public sealed class LocaleService : MonoBehaviour {
        private const string DefaultLocale = "en";

        public string CurrentLocale { get; private set; }

        public event Action<string> OnLocaleChanged;

        public void Init() {
            CurrentLocale = G.Settings.Current.Locale ?? DefaultLocale;
        }

        public void SetLocale(string locale) {
            if (CurrentLocale == locale) {
                return;
            }

            CurrentLocale = locale;
            G.Settings.Current.Locale = locale;
            G.Settings.Save();

            DialogLoader.ClearCache();

            OnLocaleChanged?.Invoke(locale);
        }
    }
}
