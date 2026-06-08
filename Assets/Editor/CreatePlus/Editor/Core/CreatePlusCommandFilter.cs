using System;
using System.Collections.Generic;

namespace CreatePlus.Core {
    /// <summary>
    /// Pure, UI-independent search/filter logic. A command matches when the query is contained
    /// (case-insensitive) in its display name, original menu path, group name, source, kind, or any
    /// alias. Filtering is a simple substring match for the MVP; ranking/highlighting can be layered
    /// on later without changing callers.
    /// </summary>
    public static class CreatePlusCommandFilter {
        /// <summary>Returns true when the command matches the query. An empty query matches everything.</summary>
        public static bool Matches(CreatePlusCommand command, string query) {
            if (command == null) {
                return false;
            }

            if (string.IsNullOrWhiteSpace(query)) {
                return true;
            }

            string q = query.Trim();

            if (Contains(command.DisplayName, q)) {
                return true;
            }

            if (Contains(command.OriginalPath, q)) {
                return true;
            }

            if (Contains(command.GroupName, q)) {
                return true;
            }

            if (Contains(command.Source, q)) {
                return true;
            }

            if (Contains(command.Kind.ToString(), q)) {
                return true;
            }

            if (command.Aliases != null) {
                for (int i = 0; i < command.Aliases.Length; i++) {
                    if (Contains(command.Aliases[i], q)) {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Returns the subset of commands matching the query, preserving input order.</summary>
        public static List<CreatePlusCommand> Filter(IEnumerable<CreatePlusCommand> commands, string query) {
            var result = new List<CreatePlusCommand>();
            if (commands == null) {
                return result;
            }

            foreach (CreatePlusCommand command in commands) {
                if (Matches(command, query)) {
                    result.Add(command);
                }
            }

            return result;
        }

        static bool Contains(string haystack, string needle) {
            if (string.IsNullOrEmpty(haystack)) {
                return false;
            }

            return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
