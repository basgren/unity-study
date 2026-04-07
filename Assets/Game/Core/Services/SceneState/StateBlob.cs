using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.Services.SceneState {
    /// <summary>
    /// In-memory state container for one saved object (identified by saveId and tier).
    /// Stores slot → field → value, where values are plain primitives (bool, float, int, float[]).
    /// The shape is intentionally JSON-serializable for future disk persistence.
    /// </summary>
    public class StateBlob {
        // slot name → field name → boxed value
        private readonly Dictionary<string, Dictionary<string, object>> slots = new();

        /// <summary>Returns a writer for the given slot, creating it if it doesn't exist.</summary>
        public IStateWriter Writer(string slot) {
            if (!slots.TryGetValue(slot, out var data)) {
                data = new Dictionary<string, object>();
                slots[slot] = data;
            }

            return new SlotAccessor(data);
        }

        /// <summary>Returns a reader for the given slot. Returns false if the slot has no data.</summary>
        public bool TryReader(string slot, out IStateReader reader) {
            if (slots.TryGetValue(slot, out var data)) {
                reader = new SlotAccessor(data);
                return true;
            }

            reader = null;
            return false;
        }

        // Single class that implements both interfaces to avoid a two-type allocation per slot access.
        private sealed class SlotAccessor : IStateWriter, IStateReader {
            private readonly Dictionary<string, object> data;

            public SlotAccessor(Dictionary<string, object> data) {
                this.data = data;
            }

            // ---- IStateWriter ----

            public void SetBool(string key, bool value) => data[key] = value;
            public void SetFloat(string key, float value) => data[key] = value;
            public void SetInt(string key, int value) => data[key] = value;
            public void SetString(string key, string value) => data[key] = value;

            // Vector2 is stored as float[2] so it round-trips through JSON without a custom converter.
            public void SetVector2(string key, Vector2 v) => data[key] = new float[] { v.x, v.y };

            // ---- IStateReader ----

            public bool TryGetBool(string key, out bool value) {
                if (data.TryGetValue(key, out var raw) && raw is bool b) {
                    value = b;
                    return true;
                }

                value = default;
                return false;
            }

            public bool TryGetFloat(string key, out float value) {
                if (data.TryGetValue(key, out var raw) && raw is float f) {
                    value = f;
                    return true;
                }

                value = default;
                return false;
            }

            public bool TryGetInt(string key, out int value) {
                if (data.TryGetValue(key, out var raw) && raw is int i) {
                    value = i;
                    return true;
                }

                value = default;
                return false;
            }

            public bool TryGetString(string key, out string value) {
                if (data.TryGetValue(key, out var raw) && raw is string s) {
                    value = s;
                    return true;
                }

                value = default;
                return false;
            }

            public bool TryGetVector2(string key, out Vector2 value) {
                if (data.TryGetValue(key, out var raw) && raw is float[] arr && arr.Length >= 2) {
                    value = new Vector2(arr[0], arr[1]);
                    return true;
                }

                value = default;
                return false;
            }
        }
    }
}
