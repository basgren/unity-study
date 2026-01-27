#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Core.Models.Inventory;
using UnityEditor;
using UnityEngine;

namespace Core.Models.Editor {
    public static class ItemIdsGenerator {
        public static void GenerateFor(InventoryItemsDef itemsDef) {
            if (itemsDef == null) {
                Debug.LogError("InventoryItemsDef is null.");
                return;
            }

            if (!itemsDef.GenerateCSharpClass) {
                return;
            }

            if (!TryGetOutput(itemsDef, out var outPath, out var className, out var ns, out var error)) {
                Debug.LogError(error, itemsDef);
                return;
            }

            var ids = ReadIds(itemsDef, out var errors);
            if (errors.Count > 0) {
                foreach (var e in errors) {
                    Debug.LogError(e, itemsDef);
                }

                Debug.LogError("C# class was NOT generated due to validation errors.", itemsDef);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outPath));

            var sb = new StringBuilder(4096);
            sb.AppendLine("// Auto-generated. Do not edit manually.");
            sb.AppendLine("using Game.Models;");
            sb.AppendLine();

            var useNamespace = !string.IsNullOrWhiteSpace(ns);
            if (useNamespace) {
                sb.AppendLine($"namespace {ns} {{");
            }

            var indent = useNamespace ? "    " : string.Empty;

            sb.AppendLine($"{indent}public static class {className} {{");
            foreach (var id in ids) {
                var name = ToPascalIdentifier(id);
                sb.AppendLine($"{indent}    public static readonly ItemId {name} = new ItemId(\"{Escape(id)}\");");
            }

            sb.AppendLine($"{indent}}}");

            if (useNamespace) {
                sb.AppendLine("}");
            }

            File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
            AssetDatabase.ImportAsset(outPath);

            Debug.Log($"Generated: {outPath}", itemsDef);
        }

        private static bool TryGetOutput(
            InventoryItemsDef itemsDef,
            out string outPath,
            out string className,
            out string ns,
            out string error) {
            outPath = null;
            className = null;
            ns = null;
            error = null;

            className = (itemsDef.CSharpClassName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(className) || !IsValidIdentifier(className)) {
                error = $"Invalid C# Class Name: '{itemsDef.CSharpClassName}'.";
                return false;
            }

            ns = (itemsDef.CSharpClassNamespace ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(ns) && !IsValidNamespace(ns)) {
                error = $"Invalid C# Class Namespace: '{itemsDef.CSharpClassNamespace}'.";
                return false;
            }

            outPath = (itemsDef.CSharpClassFile ?? string.Empty).Replace('\\', '/').Trim();
            if (string.IsNullOrEmpty(outPath)) {
                error = "C# Class File is empty. Pick a .cs file path.";
                return false;
            }

            if (!outPath.StartsWith("Assets/", StringComparison.Ordinal)) {
                error = $"C# Class File must be under Assets/: '{outPath}'.";
                return false;
            }

            if (outPath.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0) {
                error = $"C# Class File points to an Editor folder (won't be available in runtime): '{outPath}'.";
                return false;
            }

            if (!outPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) {
                error = $"C# Class File must end with .cs: '{outPath}'.";
                return false;
            }

            return true;
        }

        private static List<string> ReadIds(InventoryItemsDef itemsDef, out List<string> errors) {
            errors = new List<string>();

            var so = new SerializedObject(itemsDef);
            var itemsProp = so.FindProperty("items");
            if (itemsProp == null || !itemsProp.isArray) {
                errors.Add("Cannot find serialized field 'items'.");
                return new List<string>();
            }

            var ids = new List<string>(itemsProp.arraySize);
            var usedIds = new HashSet<string>(StringComparer.Ordinal);
            var usedNames = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < itemsProp.arraySize; i++) {
                var element = itemsProp.GetArrayElementAtIndex(i);
                var idProp = element.FindPropertyRelative("id");
                if (idProp == null) {
                    errors.Add($"items[{i}]: cannot find field 'id'.");
                    continue;
                }

                var id = (idProp.stringValue ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(id)) {
                    errors.Add($"items[{i}]: empty id.");
                    continue;
                }

                if (!usedIds.Add(id)) {
                    errors.Add($"Duplicate item id: '{id}'.");
                    continue;
                }

                var name = ToPascalIdentifier(id);
                if (!usedNames.Add(name)) {
                    errors.Add(
                        $"Identifier collision after normalization: '{name}'. Rename one of the ids (e.g. '{id}').");
                    continue;
                }

                ids.Add(id);
            }

            ids.Sort(StringComparer.Ordinal);
            return ids;
        }

        private static string ToPascalIdentifier(string id) {
            var sb = new StringBuilder(id.Length);
            var upperNext = true;

            for (var i = 0; i < id.Length; i++) {
                var c = id[i];
                if (char.IsLetterOrDigit(c)) {
                    if (sb.Length == 0 && char.IsDigit(c)) {
                        sb.Append("Item");
                    }

                    sb.Append(upperNext ? char.ToUpperInvariant(c) : c);
                    upperNext = false;
                } else {
                    upperNext = true;
                }
            }

            return sb.Length == 0 ? "Item" : sb.ToString();
        }

        private static bool IsValidIdentifier(string s) {
            if (string.IsNullOrEmpty(s)) {
                return false;
            }

            if (!(char.IsLetter(s[0]) || s[0] == '_')) {
                return false;
            }

            for (var i = 1; i < s.Length; i++) {
                var c = s[i];
                if (!(char.IsLetterOrDigit(c) || c == '_')) {
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidNamespace(string ns) {
            var parts = ns.Split('.');
            if (parts.Length == 0) {
                return false;
            }

            foreach (var p in parts) {
                if (!IsValidIdentifier(p.Trim())) {
                    return false;
                }
            }

            return true;
        }

        private static string Escape(string s) {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
#endif
