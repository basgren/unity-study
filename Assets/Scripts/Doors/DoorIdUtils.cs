using System;

namespace Doors {
    /// <summary>
    /// Utility methods for door identifier generation and validation.
    /// Allowed characters: [0-9a-zA-Z_-].
    /// </summary>
    public static class DoorIdUtils {
        public const int MinLength = 1;
        public const int MaxLength = 64;

        private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";

        // Non-cryptographic RNG. Duplicates are caught by validation.
        private static readonly Random random = new Random();

        /// <summary>
        /// Generates a random door id consisting of allowed characters.
        /// Note: This is not cryptographically secure and may have minor distribution bias.
        /// </summary>
        public static string GenerateId(int length) {
            if (length < MinLength) {
                length = MinLength;
            }

            if (length > MaxLength) {
                length = MaxLength;
            }

            var chars = new char[length];
            for (var i = 0; i < length; i++) {
                var idx = (int)(random.NextDouble() * Alphabet.Length);
                if (idx >= Alphabet.Length) {
                    idx = Alphabet.Length - 1;
                }

                chars[i] = Alphabet[idx];
            }

            return new string(chars);
        }

        /// <summary>
        /// Checks that the given id matches [0-9a-zA-Z_-] and is within length limits.
        /// </summary>
        public static bool IsValidId(string id) {
            if (string.IsNullOrEmpty(id)) {
                return false;
            }

            if (id.Length < MinLength || id.Length > MaxLength) {
                return false;
            }

            for (var i = 0; i < id.Length; i++) {
                var c = id[i];

                var ok =
                    (c >= '0' && c <= '9') ||
                    (c >= 'a' && c <= 'z') ||
                    (c >= 'A' && c <= 'Z') ||
                    c == '_' ||
                    c == '-';

                if (!ok) {
                    return false;
                }
            }

            return true;
        }
    }
}
