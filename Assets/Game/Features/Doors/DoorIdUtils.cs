using Game.Core.Utils;

namespace Game.Doors {
    /// <summary>
    /// Utility methods for door identifier generation and validation.
    /// Forwards to the shared IdUtils.
    /// </summary>
    public static class DoorIdUtils {
        public const int MinLength = IdUtils.MinLength;
        public const int MaxLength = IdUtils.MaxLength;

        public static string GenerateId(int length) => IdUtils.GenerateId(length);

        public static bool IsValidId(string id) => IdUtils.IsValidId(id);
    }
}
