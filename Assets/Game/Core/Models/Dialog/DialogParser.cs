using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Game.Core.Models.Dialog {
    /// <summary>
    /// Parses dialog JSON with human-readable string enum values.
    /// Converts <c>"type": "HasItem"</c> to <c>"type": 0</c> before deserialization.
    /// </summary>
    public static class DialogParser {
        private static readonly Regex TypeFieldPattern =
            new(@"""type""\s*:\s*""(\w+)""", RegexOptions.Compiled);

        public static DialogDef Parse(string json) {
            json = TypeFieldPattern.Replace(json, ResolveEnumValue);
            return JsonUtility.FromJson<DialogDef>(json);
        }

        private static string ResolveEnumValue(Match match) {
            var name = match.Groups[1].Value;

            if (Enum.TryParse<ConditionType>(name, out var conditionType)) {
                return $"\"type\": {(int)conditionType}";
            }

            if (Enum.TryParse<DialogActionType>(name, out var actionType)) {
                return $"\"type\": {(int)actionType}";
            }

            Debug.LogWarning($"DialogParser: unknown enum value '{name}', leaving as-is.");
            return match.Value;
        }
    }
}
