namespace Game.Editor.SpriteTools.Core {
    /// <summary>
    /// Pure string helpers for sprite naming. UI-agnostic and side-effect free, so they can be unit
    /// tested directly and reused by any front-end (IMGUI today, UI Toolkit later).
    /// </summary>
    public static class SpriteNaming {
        /// <summary>
        /// Builds "<paramref name="baseName"/>_<number>" where number = <paramref name="startIndex"/> +
        /// <paramref name="offset"/>, zero-padded to <paramref name="padWidth"/> digits
        /// (a non-positive pad width means no padding).
        /// </summary>
        public static string BuildIndexedName(string baseName, int startIndex, int offset, int padWidth) {
            var number = startIndex + offset;
            var digits = number.ToString();
            if (padWidth > 0) {
                digits = digits.PadLeft(padWidth, '0');
            }

            return $"{baseName}_{digits}";
        }

        /// <summary>
        /// Strips a trailing "_&lt;digits&gt;" suffix: "idle_03" → "idle". Names without such a suffix
        /// (and null/empty names) are returned unchanged.
        /// </summary>
        public static string StripNumericSuffix(string name) {
            if (string.IsNullOrEmpty(name)) {
                return name;
            }

            var underscore = name.LastIndexOf('_');
            if (underscore <= 0 || underscore == name.Length - 1) {
                return name;
            }

            for (int i = underscore + 1; i < name.Length; i++) {
                if (!char.IsDigit(name[i])) {
                    return name;
                }
            }

            return name.Substring(0, underscore);
        }
    }
}
