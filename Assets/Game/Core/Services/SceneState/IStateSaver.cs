using UnityEngine;

namespace Game.Core.Services.SceneState {
    /// <summary>
    /// Writes typed values into a named slot of a StateBlob.
    /// </summary>
    public interface IStateWriter {
        void SetBool(string key, bool value);
        void SetFloat(string key, float value);
        void SetInt(string key, int value);
        void SetVector2(string key, Vector2 value);
        void SetString(string key, string value);
    }

    /// <summary>
    /// Reads typed values from a named slot of a StateBlob.
    /// All TryGet methods return false and leave the out value at default if the key is missing.
    /// </summary>
    public interface IStateReader {
        bool TryGetBool(string key, out bool value);
        bool TryGetFloat(string key, out float value);
        bool TryGetInt(string key, out int value);
        bool TryGetVector2(string key, out Vector2 value);
        bool TryGetString(string key, out string value);
    }

    /// <summary>
    /// Implemented by sibling components of StateRoot to contribute one slot of saved state.
    /// </summary>
    public interface IStateSaver {
        /// <summary>Unique slot key within the containing StateRoot, e.g. "transform" or "switch".</summary>
        string Slot { get; }

        /// <summary>Writes the current state of this component to the writer.</summary>
        void Capture(IStateWriter w);

        /// <summary>Reads previously captured state and applies it to this component.</summary>
        void Restore(IStateReader r);
    }
}
