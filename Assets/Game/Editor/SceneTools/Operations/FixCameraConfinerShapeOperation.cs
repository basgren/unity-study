#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor.SceneTools.Operations {
    /// <summary>
    /// Repair operation for the Cinemachine 2 → 3 upgrade, which dropped each vcam's confiner
    /// bounding-shape reference. For every <see cref="CinemachineConfiner2D"/> in the scene it sets
    /// <c>BoundingShape2D</c> to the scene's "World/CameraConfiner" collider.
    ///
    /// The confiner lives inside the GlobalRoot prefab instance and the collider is a per-scene
    /// object, so the assignment is a prefab instance override — recorded via
    /// <see cref="PrefabUtility.RecordPrefabInstancePropertyModifications"/> so it persists on save.
    /// Scenes without a confiner (e.g. menus) are skipped; a confiner with no matching collider is
    /// reported as an error and left untouched rather than cleared.
    /// </summary>
    public sealed class FixCameraConfinerShapeOperation : ISceneOperation {
        private const string WorldRootName = "World";
        private const string ConfinerObjectName = "CameraConfiner";

        public string Category => "Camera";
        public string DisplayName => "Fix Camera Confiner Shape";
        public bool Mutates => true;

        public SceneOperationResult Run(Scene scene, ISceneOperationLog log) {
            var confiners = FindConfiners(scene);
            if (confiners.Count == 0) {
                log.Info("No CinemachineConfiner2D in scene; skipped.");
                return SceneOperationResult.Fix(0);
            }

            Collider2D shape = FindCameraConfinerCollider(scene);
            if (shape == null) {
                log.Error($"Found {confiners.Count} CinemachineConfiner2D but no " +
                    $"'{WorldRootName}/{ConfinerObjectName}' collider to assign; left untouched.");
                return SceneOperationResult.Fix(0);
            }

            int changes = 0;
            foreach (var confiner in confiners) {
                if (confiner.BoundingShape2D == shape) {
                    continue;
                }

                confiner.BoundingShape2D = shape;
                // The confiner is part of the GlobalRoot prefab instance, so the change must be
                // recorded as an instance override; otherwise it is dropped when the scene is saved.
                if (PrefabUtility.IsPartOfPrefabInstance(confiner)) {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(confiner);
                } else {
                    EditorUtility.SetDirty(confiner);
                }

                log.Info($"Set BoundingShape2D = '{shape.name}' on '{GetHierarchyPath(confiner.transform)}'.", confiner);
                changes++;
            }

            if (changes == 0) {
                log.Info("Confiner bounding shape already correct.");
            }

            return SceneOperationResult.Fix(changes);
        }

        private static List<CinemachineConfiner2D> FindConfiners(Scene scene) {
            var result = new List<CinemachineConfiner2D>();
            foreach (var root in scene.GetRootGameObjects()) {
                result.AddRange(root.GetComponentsInChildren<CinemachineConfiner2D>(true));
            }

            return result;
        }

        private static Collider2D FindCameraConfinerCollider(Scene scene) {
            foreach (var root in scene.GetRootGameObjects()) {
                if (root.name != WorldRootName) {
                    continue;
                }

                Transform confinerObject = FindDescendant(root.transform, ConfinerObjectName);
                if (confinerObject != null) {
                    return confinerObject.GetComponent<Collider2D>();
                }
            }

            return null;
        }

        private static Transform FindDescendant(Transform parent, string name) {
            if (parent.name == name) {
                return parent;
            }

            for (int i = 0; i < parent.childCount; i++) {
                Transform found = FindDescendant(parent.GetChild(i), name);
                if (found != null) {
                    return found;
                }
            }

            return null;
        }

        private static string GetHierarchyPath(Transform t) {
            string path = t.name;
            while (t.parent != null) {
                t = t.parent;
                path = t.name + "/" + path;
            }

            return path;
        }
    }
}
#endif
