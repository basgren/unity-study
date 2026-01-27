#if UNITY_EDITOR
using System;
using System.Linq;
using Core.Models.Inventory;
using UnityEditor;

namespace Core.Models.Editor {
    public sealed class ItemIdsAutoGenerator : UnityEditor.AssetModificationProcessor {
        private static bool queued;

        private static string[] OnWillSaveAssets(string[] paths) {
            if (queued || paths == null || paths.Length == 0) {
                return paths;
            }

            // Берём только те пути, где сохраняется InventoryItemsDef
            var targetPaths = paths.Where(p =>
                p.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) &&
                AssetDatabase.GetMainAssetTypeAtPath(p) == typeof(InventoryItemsDef)).ToArray();

            if (targetPaths.Length == 0) {
                return paths;
            }

            // Планируем генерацию ПОСЛЕ сохранения
            queued = true;
            EditorApplication.delayCall += () => {
                queued = false;

                foreach (var p in targetPaths) {
                    var def = AssetDatabase.LoadAssetAtPath<InventoryItemsDef>(p);
                    if (def == null) {
                        continue;
                    }

                    // Генератор сам проверит def.GenerateCSharpClass и заполненность File/Name/Namespace.
                    ItemIdsGenerator.GenerateFor(def);
                }
            };

            return paths;
        }
    }
}
#endif
