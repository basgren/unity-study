using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Editor.ObjectBrush {
    /// <summary>
    /// Shared helpers for the Object Brush windows: loading the shared config asset
    /// and resolving the World-root parenting convention inside a scene.
    /// </summary>
    public static class ObjectBrushUtility {
        /// <summary>
        /// Loads the shared <see cref="ObjectBrushConfig"/> asset, optionally creating it
        /// (and any missing folders) when it does not exist yet.
        /// </summary>
        public static ObjectBrushConfig LoadOrCreateConfig(bool createIfNotFound) {
            string assetPath = ObjectBrushConfig.DefaultAssetPath;
            ObjectBrushConfig config = AssetDatabase.LoadAssetAtPath<ObjectBrushConfig>(assetPath);

            if (config == null && createIfNotFound) {
                EnsureFolderExists(Path.GetDirectoryName(assetPath));
                config = ScriptableObject.CreateInstance<ObjectBrushConfig>();
                AssetDatabase.CreateAsset(config, assetPath);
                AssetDatabase.SaveAssets();
            }

            return config;
        }

        /// <summary>
        /// Returns the parent path for a category, falling back to the category name
        /// when no explicit path is set.
        /// </summary>
        public static string ResolveParentPath(ObjectBrushProfile.BiomeCategory category) {
            if (category == null) {
                return null;
            }

            string path = string.IsNullOrEmpty(category.parentPath) ? null : category.parentPath.Trim();
            if (string.IsNullOrEmpty(path)) {
                path = category.name;
            }

            return string.IsNullOrEmpty(path) ? null : path.Trim();
        }

        /// <summary>
        /// Finds (or creates) the transform addressed by
        /// "<paramref name="worldRootName"/>/<paramref name="relativePath"/>" in the scene.
        /// All created objects are registered with Undo. Returns null only when both the
        /// root name and the relative path are empty (place directly at scene root).
        /// </summary>
        public static Transform ResolveOrCreateParent(Scene scene, string worldRootName, string relativePath) {
            List<string> segments = new List<string>();

            if (!string.IsNullOrEmpty(worldRootName) && !string.IsNullOrWhiteSpace(worldRootName)) {
                segments.Add(worldRootName.Trim());
            }

            if (!string.IsNullOrEmpty(relativePath)) {
                foreach (string part in relativePath.Split('/')) {
                    string trimmed = part.Trim();
                    if (!string.IsNullOrEmpty(trimmed)) {
                        segments.Add(trimmed);
                    }
                }
            }

            if (segments.Count == 0) {
                return null;
            }

            Transform current = FindRoot(scene, segments[0]);
            if (current == null) {
                current = CreateChild(segments[0], null, scene);
            }

            for (int i = 1; i < segments.Count; i++) {
                Transform child = current.Find(segments[i]);
                if (child == null) {
                    child = CreateChild(segments[i], current, scene);
                }

                current = child;
            }

            return current;
        }

        private static void EnsureFolderExists(string dir) {
            if (string.IsNullOrEmpty(dir) || AssetDatabase.IsValidFolder(dir)) {
                return;
            }

            string[] parts = dir.Replace("\\", "/").Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++) {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static Transform FindRoot(Scene scene, string name) {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++) {
                if (roots[i].name == name) {
                    return roots[i].transform;
                }
            }

            return null;
        }

        private static Transform CreateChild(string name, Transform parent, Scene scene) {
            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Object Brush Parent");

            if (parent != null) {
                Undo.SetTransformParent(go.transform, parent, "Create Object Brush Parent");
            } else {
                SceneManager.MoveGameObjectToScene(go, scene);
            }

            go.transform.localPosition = Vector3.zero;
            return go.transform;
        }
    }
}
