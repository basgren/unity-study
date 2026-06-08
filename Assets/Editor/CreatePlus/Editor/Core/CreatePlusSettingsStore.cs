using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CreatePlus.Core {
    /// <summary>
    /// Loads, queries, mutates and persists <see cref="CreatePlusSettings"/>. This is the single
    /// source of truth for per-user command state (favorites, pins, hidden, collapsed groups, recent,
    /// usage). UI code reads and writes state only through this store, never directly on the settings
    /// object or on UI controls.
    ///
    /// Storage: a JSON blob in EditorPrefs under <see cref="PrefsKey"/>. The key carries a version so a
    /// future migration to UserSettings/CreatePlus.user.json can be done cleanly.
    /// </summary>
    public static class CreatePlusSettingsStore {
        const string PrefsKey = "CreatePlus.Settings.v1";
        const int MaxRecent = 5;

        static CreatePlusSettings settings;

        /// <summary>Raised whenever settings change so listeners (the window) can repaint.</summary>
        public static event Action Changed;

        static CreatePlusSettings Settings {
            get {
                if (settings == null) {
                    Load();
                }

                return settings;
            }
        }

        /// <summary>Builds a stable group key from a panel and group name.</summary>
        public static string GroupKey(string panelName, string groupName) {
            return panelName + "/" + groupName;
        }

        // ---- Favorites -------------------------------------------------------------------------

        public static bool IsFavorite(string id) {
            return Settings.favorites.Contains(id);
        }

        public static void ToggleFavorite(string id) {
            if (string.IsNullOrEmpty(id)) {
                return;
            }

            if (!Settings.favorites.Remove(id)) {
                Settings.favorites.Add(id);
            }

            Save();
        }

        public static IReadOnlyList<string> GetFavorites() {
            return Settings.favorites;
        }

        // ---- Pinning ---------------------------------------------------------------------------

        public static bool IsPinned(string id) {
            return Settings.pinned.Contains(id);
        }

        public static void TogglePinned(string id) {
            if (string.IsNullOrEmpty(id)) {
                return;
            }

            if (!Settings.pinned.Remove(id)) {
                Settings.pinned.Add(id);
            }

            Save();
        }

        // ---- Hiding ----------------------------------------------------------------------------

        public static bool IsHidden(string id) {
            return Settings.hidden.Contains(id);
        }

        public static void ToggleHidden(string id) {
            if (string.IsNullOrEmpty(id)) {
                return;
            }

            if (!Settings.hidden.Remove(id)) {
                Settings.hidden.Add(id);
            }

            Save();
        }

        public static bool HasHidden() {
            return Settings.hidden.Count > 0;
        }

        // ---- Collapsed groups ------------------------------------------------------------------

        /// <summary>
        /// Returns whether a group is collapsed. If the user has overridden this group, that value is
        /// returned; otherwise the caller-supplied default is used. The default-collapsed policy lives
        /// with the view model (which knows the group hierarchy), keeping this store policy-free.
        /// </summary>
        public static bool IsGroupCollapsed(string groupKey, bool defaultCollapsed) {
            int index = Settings.collapsedOverrideKeys.IndexOf(groupKey);
            if (index >= 0 && index < Settings.collapsedOverrideValues.Count) {
                return Settings.collapsedOverrideValues[index];
            }

            return defaultCollapsed;
        }

        public static void SetGroupCollapsed(string groupKey, bool collapsed) {
            int index = Settings.collapsedOverrideKeys.IndexOf(groupKey);
            if (index >= 0) {
                Settings.collapsedOverrideValues[index] = collapsed;
            } else {
                Settings.collapsedOverrideKeys.Add(groupKey);
                Settings.collapsedOverrideValues.Add(collapsed);
            }

            Save();
        }

        // ---- Recent ----------------------------------------------------------------------------

        public static void AddRecent(string id) {
            if (string.IsNullOrEmpty(id)) {
                return;
            }

            Settings.recent.Remove(id);
            Settings.recent.Insert(0, id);
            while (Settings.recent.Count > MaxRecent) {
                Settings.recent.RemoveAt(Settings.recent.Count - 1);
            }

            Save();
        }

        public static IReadOnlyList<string> GetRecent() {
            return Settings.recent;
        }

        // ---- Usage -----------------------------------------------------------------------------

        public static void RecordUsage(string id) {
            if (string.IsNullOrEmpty(id)) {
                return;
            }

            CreatePlusSettings.UsageEntry entry = FindUsage(id);
            if (entry == null) {
                entry = new CreatePlusSettings.UsageEntry { id = id };
                Settings.usage.Add(entry);
            }

            entry.count++;
            entry.lastUsedUtcTicks = DateTime.UtcNow.Ticks;
            Save();
        }

        public static int GetUsageCount(string id) {
            CreatePlusSettings.UsageEntry entry = FindUsage(id);
            return entry != null ? entry.count : 0;
        }

        public static DateTime GetLastUsedUtc(string id) {
            CreatePlusSettings.UsageEntry entry = FindUsage(id);
            return entry != null ? new DateTime(entry.lastUsedUtcTicks, DateTimeKind.Utc) : DateTime.MinValue;
        }

        static CreatePlusSettings.UsageEntry FindUsage(string id) {
            for (int i = 0; i < Settings.usage.Count; i++) {
                if (Settings.usage[i].id == id) {
                    return Settings.usage[i];
                }
            }

            return null;
        }

        // ---- Maintenance -----------------------------------------------------------------------

        /// <summary>Clears all per-command state for one command (the "Reset Command Settings" action).</summary>
        public static void ResetCommand(string id) {
            if (string.IsNullOrEmpty(id)) {
                return;
            }

            Settings.favorites.Remove(id);
            Settings.pinned.Remove(id);
            Settings.hidden.Remove(id);
            Settings.recent.Remove(id);
            CreatePlusSettings.UsageEntry entry = FindUsage(id);
            if (entry != null) {
                Settings.usage.Remove(entry);
            }

            Save();
        }

        /// <summary>Wipes all Create Plus settings back to defaults.</summary>
        public static void ResetAll() {
            settings = new CreatePlusSettings();
            EditorPrefs.DeleteKey(PrefsKey);
            RaiseChanged();
        }

        // ---- Persistence -----------------------------------------------------------------------

        static void Load() {
            string json = EditorPrefs.GetString(PrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json)) {
                settings = new CreatePlusSettings();
                return;
            }

            try {
                settings = JsonUtility.FromJson<CreatePlusSettings>(json) ?? new CreatePlusSettings();
            } catch (Exception e) {
                Debug.LogWarning("[Create Plus] Failed to read settings, using defaults: " + e.Message);
                settings = new CreatePlusSettings();
            }
        }

        static void Save() {
            string json = JsonUtility.ToJson(Settings);
            EditorPrefs.SetString(PrefsKey, json);
            RaiseChanged();
        }

        static void RaiseChanged() {
            Action handler = Changed;
            if (handler != null) {
                handler();
            }
        }
    }
}
