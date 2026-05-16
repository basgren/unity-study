#if UNITY_EDITOR
using Game.Core.Utils;
using Game.Doors.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Features.Doors.Editor {
    /// <summary>
    /// Editor utility window used to rename an entrance id and update references.
    /// Open scenes are marked dirty (no auto-save); prefabs are saved automatically.
    /// </summary>
    public sealed class EntranceChangeIdWindow : EditorWindow {
        private Entrance entrance;
        private string newId;
        private bool updateAllScenesOnDisk;

        public static void Show(Entrance entrance) {
            var w = CreateInstance<EntranceChangeIdWindow>();
            w.entrance = entrance;
            w.newId = entrance != null ? entrance.EntranceId : string.Empty;
            w.titleContent = new GUIContent("Change Entrance ID");
            w.minSize = new Vector2(520, 170);
            w.maxSize = new Vector2(520, 170);
            w.ShowUtility();
        }

        private void OnGUI() {
            if (entrance == null) {
                EditorGUILayout.HelpBox("Entrance reference is missing.", MessageType.Error);
                return;
            }

            EditorGUILayout.HelpBox(
                "Allowed: [0-9a-zA-Z_-], length 1..64\n" +
                "Change ID updates references in OPEN scenes (mark dirty) and in prefabs (auto-saved).",
                MessageType.Info
            );

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Current ID", entrance.EntranceId);
            EditorGUI.EndDisabledGroup();

            newId = EditorGUILayout.TextField("New ID", newId);

            EditorGUILayout.Space();

            updateAllScenesOnDisk = EditorGUILayout.ToggleLeft(
                "Find and update references in ALL scenes of the project (auto-save those scenes)",
                updateAllScenesOnDisk
            );

            if (updateAllScenesOnDisk) {
                EditorGUILayout.HelpBox(
                    "This may take time on large projects. Open scenes will NOT be auto-saved; they will only be marked dirty.",
                    MessageType.Warning
                );
            }

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Random")) {
                    newId = IdUtils.GenerateId(5);
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
            newId = (newId ?? string.Empty).Trim();

            if (!IdUtils.IsValidId(newId)) {
                EditorUtility.DisplayDialog("Invalid ID", "Allowed: [0-9a-zA-Z_-], length 1..64.", "OK");
                return;
            }

            var oldId = entrance.EntranceId;
            if (newId == oldId) {
                Close();
                return;
            }

            var scene = entrance.gameObject.scene;
            if (!scene.IsValid()) {
                EditorUtility.DisplayDialog("Error", "Entrance is not in a valid scene.", "OK");
                return;
            }

            if (!EntranceValidator.IsEntranceIdUniqueInScene(scene, entrance, newId)) {
                EditorUtility.DisplayDialog("Duplicate ID", "This ID already exists in the same scene.", "OK");
                return;
            }

            var sceneGuid = DoorEditorUtils.GetSceneGuid(scene.path);
            if (string.IsNullOrWhiteSpace(sceneGuid)) {
                EditorUtility.DisplayDialog("Error", "Failed to resolve scene GUID.", "OK");
                return;
            }

            var changedOpenScenesLinks = 0;
            var changedPrefabLinks = 0;
            var changedDiskSceneLinks = 0;
            var changedDiskScenes = 0;

            EntranceProjectUpdater.ReplaceReferencesInOpenScenes(sceneGuid, oldId, newId, ref changedOpenScenesLinks);
            EntranceProjectUpdater.ReplaceReferencesInAllPrefabs(sceneGuid, oldId, newId, ref changedPrefabLinks);

            if (updateAllScenesOnDisk) {
                EntranceProjectUpdater.ReplaceReferencesInAllScenesOnDisk(
                    sceneGuid,
                    oldId,
                    newId,
                    ref changedDiskSceneLinks,
                    ref changedDiskScenes
                );
            }

            Undo.RecordObject(entrance, "Change Entrance ID");
            entrance.EditorSetEntranceId(newId);
            EditorUtility.SetDirty(entrance);

            EditorSceneManager.MarkSceneDirty(scene);

            EditorUtility.DisplayDialog(
                "Done",
                $"Entrance ID changed: {oldId} -> {newId}\n" +
                $"Updated references:\n" +
                $"- Open scenes: {changedOpenScenesLinks} (scenes marked dirty)\n" +
                $"- Prefabs: {changedPrefabLinks} (prefabs saved)\n" +
                (updateAllScenesOnDisk
                    ? $"- Project scenes (auto-saved): {changedDiskSceneLinks} links in {changedDiskScenes} scenes\n"
                    : string.Empty),
                "OK"
            );

            Close();
        }
    }
}
#endif
