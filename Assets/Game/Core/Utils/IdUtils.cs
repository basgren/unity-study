using System;

namespace Game.Core.Utils {
    /// <summary>
    /// Shared utility methods for stable ID generation and validation.
    /// Allowed characters: [0-9a-zA-Z_-], length 1..64.
    /// </summary>
    public static class IdUtils {
        public const int MinLength = 1;
        public const int MaxLength = 64;

        private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";

        // Non-cryptographic RNG. Duplicates are caught by validation.
        private static readonly Random random = new Random();

        /// <summary>
        /// Generates a random id consisting of allowed characters.
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
        /// Parses a portal id (door/entrance) stored as a numeric string.
        /// Returns true if the id is a positive integer (rejects legacy strings like "Door_abc12").
        /// </summary>
        public static bool TryParsePortalId(string id, out int value) {
            value = 0;
            if (string.IsNullOrEmpty(id)) {
                return false;
            }

            if (!int.TryParse(id, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out value)) {
                return false;
            }

            return value > 0;
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