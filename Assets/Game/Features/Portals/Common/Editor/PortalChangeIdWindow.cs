#if UNITY_EDITOR
using System;
using System.Globalization;
using Game.Core.Utils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Features.Portals.Common.Editor {
    /// <summary>
    /// Single editor window for renaming a portal id (Door, Entrance, ...).
    /// The caller provides the portal object and a setter that writes the new id to the
    /// kind-specific private field (Door.EditorSetDoorId / Entrance.EditorSetEntranceId).
    /// </summary>
    public sealed class PortalChangeIdWindow : EditorWindow {
        private UnityEngine.Object portalObj;
        private IPortal portal;
        private PortalKind kind;
        private Action<string> applySetter;
        private int newId;
        private bool updateAllScenesOnDisk;

        public static void Show(UnityEngine.Object portalObj, Action<string> applySetter) {
            var p = portalObj as IPortal;
            var k = PortalKindRegistry.GetForComponent(portalObj);
            if (p == null || k == null || applySetter == null) {
                return;
            }

            var w = CreateInstance<PortalChangeIdWindow>();
            w.portalObj = portalObj;
            w.portal = p;
            w.kind = k;
            w.applySetter = applySetter;
            w.newId = TryParseInt(p.Id);
            w.titleContent = new GUIContent($"Change {k.KindName} ID");
            w.minSize = new Vector2(520, 170);
            w.maxSize = new Vector2(520, 170);
            w.ShowUtility();
        }

        private void OnGUI() {
            if (portalObj == null || portal == null || kind == null) {
                EditorGUILayout.HelpBox("Portal reference is missing.", MessageType.Error);
                return;
            }

            EditorGUILayout.HelpBox(
                $"{kind.KindName} ID is a positive integer, unique within the scene.\n" +
                "Updates references in OPEN scenes (mark dirty) and in prefabs (auto-saved).",
                MessageType.Info);

            using (new EditorGUI.DisabledGroupScope(true)) {
                EditorGUILayout.TextField("Current ID", portal.Id);
            }

            newId = EditorGUILayout.IntField("New ID", newId);

            EditorGUILayout.Space();

            updateAllScenesOnDisk = EditorGUILayout.ToggleLeft(
                "Find and update references in ALL scenes of the project (auto-save those scenes)",
                updateAllScenesOnDisk);

            if (updateAllScenesOnDisk) {
                EditorGUILayout.HelpBox(
                    "This may take time on large projects. Open scenes will NOT be auto-saved; they will only be marked dirty.",
                    MessageType.Warning);
            }

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Next free")) {
                    newId = NextFreeId();
                }

                if (GUILayout.Button("Cancel")) {
                    Close();
                }

                if (GUILayout.Button("Apply")) {
                    Apply();
                }
            }
        }

        private void Apply() {
            if (newId <= 0) {
                EditorUtility.DisplayDialog("Invalid ID", $"{kind.KindName} ID must be a positive integer.", "OK");
                return;
            }

            var oldId = portal.Id;
            var newIdString = newId.ToString(CultureInfo.InvariantCulture);
            if (newIdString == oldId) {
                Close();
                return;
            }

            var ownerComponent = portalObj as Component;
            var scene = ownerComponent != null ? ownerComponent.gameObject.scene : default;
            if (!scene.IsValid()) {
                EditorUtility.DisplayDialog("Error", $"{kind.KindName} is not in a valid scene.", "OK");
                return;
            }

            if (!PortalValidator.IsIdUniqueInScene(kind, scene, portal, newIdString)) {
                EditorUtility.DisplayDialog("Duplicate ID", "This ID already exists in the same scene.", "OK");
                return;
            }

            var sceneGuid = PortalEditorUtils.GetSceneGuid(scene.path);
            if (string.IsNullOrWhiteSpace(sceneGuid)) {
                EditorUtility.DisplayDialog("Error", "Failed to resolve scene GUID.", "OK");
                return;
            }

            var changedOpenScenes = 0;
            var changedPrefabs = 0;
            var changedDiskLinks = 0;
            var changedDiskScenes = 0;

            PortalProjectUpdater.ReplaceReferencesInOpenScenes(kind, sceneGuid, oldId, newIdString,
                ref changedOpenScenes);
            PortalProjectUpdater.ReplaceReferencesInAllPrefabs(kind, sceneGuid, oldId, newIdString,
                ref changedPrefabs);

            if (updateAllScenesOnDisk) {
                PortalProjectUpdater.ReplaceReferencesInAllScenesOnDisk(kind, sceneGuid, oldId, newIdString,
                    ref changedDiskLinks, ref changedDiskScenes);
            }

            Undo.RecordObject(portalObj, $"Change {kind.KindName} ID");
            applySetter(newIdString);
            EditorUtility.SetDirty(portalObj);

            EditorSceneManager.MarkSceneDirty(scene);

            EditorUtility.DisplayDialog(
                "Done",
                $"{kind.KindName} ID changed: {oldId} -> {newIdString}\n" +
                $"Updated references:\n" +
                $"- Open scenes: {changedOpenScenes} (scenes marked dirty)\n" +
                $"- Prefabs: {changedPrefabs} (prefabs saved)\n" +
                (updateAllScenesOnDisk
                    ? $"- Project scenes (auto-saved): {changedDiskLinks} links in {changedDiskScenes} scenes\n"
                    : string.Empty),
                "OK");

            Close();
        }

        private int NextFreeId() {
            var ownerComponent = portalObj as Component;
            if (ownerComponent == null) {
                return 1;
            }

            var scene = ownerComponent.gameObject.scene;
            int max = 0;
            foreach (var p in kind.GetPortalsInScene(scene)) {
                if (p == null || ReferenceEquals(p, portal)) {
                    continue;
                }

                if (IdUtils.TryParsePortalId(p.Id, out var v) && v > max) {
                    max = v;
                }
            }

            return max + 1;
        }

        private static int TryParseInt(string s) {
            if (IdUtils.TryParsePortalId(s, out var value)) {
                return value;
            }

            return 1;
        }
    }
}
#endif
