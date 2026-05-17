using System;
using System.Collections.Generic;
using Game.Editor.SceneTopology.Model;
using Game.Features.Portals.Common;
using Game.Features.Portals.Doors;
using Game.Features.Portals.Entrance;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor.SceneTopology {
    /// <summary>
    /// Writes a symmetric link between two portals: source ↔ target. Both sides receive
    /// each other's scene reference + portal id. Scenes are opened additively only if they
    /// aren't already loaded, saved on completion, and closed if we opened them.
    /// Refuses to run while any open scene is dirty so we never co-mingle our edit with the
    /// user's unsaved work.
    /// </summary>
    public static class SceneTopologyLinker {
        public struct PortalRef {
            public string ScenePath;
            public PortalKindRef Kind;
            public string PortalId;
        }

        /// <summary>
        /// Clears the link on a single portal (one side only — the partner is left untouched and may
        /// become asymmetric). Returns true if the portal is now unlinked (including the case where
        /// it was already empty, in which case no scene write happens).
        /// </summary>
        public static bool RemoveLink(PortalRef p, out string error) {
            error = null;

            if (HasDirtyOpenScene()) {
                error = "Save your open scene changes before removing a link.";
                return false;
            }

            if (string.IsNullOrEmpty(p.ScenePath)) {
                error = "Missing scene path.";
                return false;
            }

            var openedByUs = new List<Scene>();
            try {
                var scene = LoadOrGetScene(p.ScenePath, openedByUs);
                var portal = FindPortalComponent(scene, p.Kind, p.PortalId);
                if (portal == null) {
                    error = $"Portal {p.Kind} #{p.PortalId} not found in {p.ScenePath}.";
                    return false;
                }

                // Cheap check: if the link is already empty, skip the dirty/save cycle entirely.
                var so = new SerializedObject(portal);
                var linkProp = so.FindProperty("link");
                if (linkProp == null) {
                    error = $"Portal {portal.GetType().Name} has no 'link' field.";
                    return false;
                }

                var sceneRefProp = linkProp.FindPropertyRelative("targetScene");
                var existingGuid = sceneRefProp.FindPropertyRelative("sceneGuid").stringValue;
                var existingTargetId = linkProp.FindPropertyRelative("targetId").stringValue;
                if (string.IsNullOrEmpty(existingGuid) && string.IsNullOrEmpty(existingTargetId)) {
                    return true;
                }

                SetLink(portal, string.Empty, string.Empty, string.Empty);

                if (!EditorSceneManager.SaveScene(scene)) {
                    error = "Failed to save " + scene.path;
                    return false;
                }

                AssetDatabase.SaveAssets();
                return true;
            } catch (Exception ex) {
                error = ex.Message;
                Debug.LogException(ex);
                return false;
            } finally {
                for (var i = 0; i < openedByUs.Count; i++) {
                    var s = openedByUs[i];
                    if (s.IsValid() && s.isLoaded) {
                        EditorSceneManager.CloseScene(s, removeScene: true);
                    }
                }
            }
        }

        public static bool LinkSymmetric(PortalRef a, PortalRef b, out string error) {
            error = null;

            if (HasDirtyOpenScene()) {
                error = "Save your open scene changes before creating a link.";
                return false;
            }

            if (a.Kind != b.Kind) {
                error = "Portals are different kinds; link aborted.";
                return false;
            }

            if (string.IsNullOrEmpty(a.ScenePath) || string.IsNullOrEmpty(b.ScenePath)) {
                error = "Missing scene path on source or target.";
                return false;
            }

            var sameScene = a.ScenePath == b.ScenePath;
            if (sameScene && string.Equals(a.PortalId, b.PortalId, StringComparison.Ordinal)) {
                error = "Cannot link a portal to itself.";
                return false;
            }

            var openedByUs = new List<Scene>();
            try {
                var sceneA = LoadOrGetScene(a.ScenePath, openedByUs);
                var sceneB = sameScene ? sceneA : LoadOrGetScene(b.ScenePath, openedByUs);

                var portalA = FindPortalComponent(sceneA, a.Kind, a.PortalId);
                if (portalA == null) {
                    error = $"Portal {a.Kind} #{a.PortalId} not found in {a.ScenePath}.";
                    return false;
                }

                var portalB = FindPortalComponent(sceneB, b.Kind, b.PortalId);
                if (portalB == null) {
                    error = $"Portal {b.Kind} #{b.PortalId} not found in {b.ScenePath}.";
                    return false;
                }

                var aGuid = AssetDatabase.AssetPathToGUID(a.ScenePath);
                var bGuid = AssetDatabase.AssetPathToGUID(b.ScenePath);

                SetLink(portalA, bGuid, b.ScenePath, b.PortalId);
                SetLink(portalB, aGuid, a.ScenePath, a.PortalId);

                if (!EditorSceneManager.SaveScene(sceneA)) {
                    error = "Failed to save " + sceneA.path;
                    return false;
                }

                if (!sameScene && !EditorSceneManager.SaveScene(sceneB)) {
                    error = "Failed to save " + sceneB.path;
                    return false;
                }

                AssetDatabase.SaveAssets();
                return true;
            } catch (Exception ex) {
                error = ex.Message;
                Debug.LogException(ex);
                return false;
            } finally {
                for (var i = 0; i < openedByUs.Count; i++) {
                    var s = openedByUs[i];
                    if (s.IsValid() && s.isLoaded) {
                        EditorSceneManager.CloseScene(s, removeScene: true);
                    }
                }
            }
        }

        private static Scene LoadOrGetScene(string path, List<Scene> openedTracker) {
            for (var i = 0; i < EditorSceneManager.sceneCount; i++) {
                var s = EditorSceneManager.GetSceneAt(i);
                if (s.IsValid() && s.isLoaded && s.path == path) {
                    return s;
                }
            }

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            openedTracker.Add(scene);
            return scene;
        }

        private static Component FindPortalComponent(Scene scene, PortalKindRef kind, string portalId) {
            switch (kind) {
                case PortalKindRef.Door:
                    return PortalUtils.FindPortalByIdInScene<Door>(scene, portalId);
                case PortalKindRef.Entrance:
                    return PortalUtils.FindPortalByIdInScene<Entrance>(scene, portalId);
                default:
                    return null;
            }
        }

        private static void SetLink(Component portal, string targetSceneGuid, string targetScenePath, string targetPortalId) {
            // SerializedProperty path matches PortalLink: { SceneReference targetScene; string targetId; }
            // SceneReference contains: { string sceneGuid; string scenePath; }.
            // Both Door and Entrance store the link in a private field called `link`.
            var so = new SerializedObject(portal);
            var linkProp = so.FindProperty("link");
            if (linkProp == null) {
                throw new InvalidOperationException(
                    $"Portal component {portal.GetType().Name} has no 'link' serialized field.");
            }

            var sceneRefProp = linkProp.FindPropertyRelative("targetScene");
            sceneRefProp.FindPropertyRelative("sceneGuid").stringValue = targetSceneGuid;
            sceneRefProp.FindPropertyRelative("scenePath").stringValue = targetScenePath;
            linkProp.FindPropertyRelative("targetId").stringValue = targetPortalId;
            // No-undo: the scene is saved immediately after, so undo would leave the file out of
            // sync with the in-memory state in a confusing way.
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(portal);
            EditorSceneManager.MarkSceneDirty(portal.gameObject.scene);
        }

        private static bool HasDirtyOpenScene() {
            for (var i = 0; i < EditorSceneManager.sceneCount; i++) {
                var s = EditorSceneManager.GetSceneAt(i);
                if (s.isDirty) {
                    return true;
                }
            }

            return false;
        }
    }
}
