using System;
using System.Collections.Generic;

namespace CreatePlus.Core {
    /// <summary>
    /// Serializable container for all persisted user preferences. Uses plain lists so it can be
    /// round-tripped with Unity's <c>JsonUtility</c> (which does not support dictionaries).
    /// Stored as JSON under a single EditorPrefs key; see <see cref="CreatePlusSettingsStore"/>.
    /// </summary>
    [Serializable]
    public sealed class CreatePlusSettings {
        /// <summary>Command ids marked as favorites, in the order they were added.</summary>
        public List<string> favorites = new List<string>();

        /// <summary>Command ids pinned inside their group.</summary>
        public List<string> pinned = new List<string>();

        /// <summary>Command ids hidden from the palette.</summary>
        public List<string> hidden = new List<string>();

        /// <summary>Group keys ("Panel/Group") whose collapsed state was explicitly overridden by the user.</summary>
        public List<string> collapsedOverrideKeys = new List<string>();

        /// <summary>Parallel to <see cref="collapsedOverrideKeys"/>: the overridden collapsed value.</summary>
        public List<bool> collapsedOverrideValues = new List<bool>();

        /// <summary>Recently executed command ids, most recent first.</summary>
        public List<string> recent = new List<string>();

        /// <summary>Usage statistics, one entry per command that has been executed.</summary>
        public List<UsageEntry> usage = new List<UsageEntry>();

        /// <summary>Per-command usage counter and last-used timestamp.</summary>
        [Serializable]
        public sealed class UsageEntry {
            public string id;
            public int count;
            public long lastUsedUtcTicks;
        }
    }
}
