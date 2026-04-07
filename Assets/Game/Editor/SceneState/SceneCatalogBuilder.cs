#if UNITY_EDITOR
using Game.Core.Services.SceneState;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor.SceneState {
    /// <summary>
    /// Rebuilds the SceneCatalog ScriptableObject from the project's build settings.
    /// Run manually via Tools → Scene State → Rebuild Scene Catalog, or automatically
    /// before each build via IPreprocessBuildWithReport.
    /// </summary>
    public sealed class SceneCatalogBuilder : IPreprocessBuildWithReport {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) {
            RebuildCatalog();
        }

        [MenuItem("Tools/Scene State/Rebuild Scene Catalog")]
        public static void RebuildCatalog() {
            var catalog = FindCatalog();
            if (catalog == null) {
                Debug.LogError(
                    "[SceneCatalogBuilder] No SceneCatalog asset found in the project. " +
                    "Create one via Assets → Create → Config → Scene Catalog, then assign it to MainConfig."
                );
                return;
            }

            var buildScenes = EditorBuildSettings.scenes;

            var so = new SerializedObject(catalog);
            var scenesProp = so.FindProperty("scenes");
            scenesProp.arraySize = buildScenes.Length;

            for (int i = 0; i < buildScenes.Length; i++) {
                var path = buildScenes[i].path;
                var guid = AssetDatabase.AssetPathToGUID(path);
                var elem = scenesProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("sceneGuid").stringValue = guid;
                elem.FindPropertyRelative("scenePath").stringValue = path;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            catalog.RebuildIndex();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            Debug.Log($"[SceneCatalogBuilder] Rebuilt catalog with {buildScenes.Length} scene(s).");
        }

        private static SceneCatalog FindCatalog() {
            var guids = AssetDatabase.FindAssets("t:SceneCatalog");
            if (guids.Length == 0) {
                return null;
            }

            if (guids.Length > 1) {
                Debug.LogWarning("[SceneCatalogBuilder] Multiple SceneCatalog assets found; using the first one.");
            }

            return AssetDatabase.LoadAssetAtPath<SceneCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
#endif
