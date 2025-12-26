#if UNITY_EDITOR
using Doors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Editor.Doors {
    /// <summary>
    /// Editor utility window used to rename a door id and update references.
    /// Open scenes are marked dirty (no auto-save); prefabs are saved automatically.
    /// </summary>
    public sealed class DoorChangeIdWindow : EditorWindow {
        private Door door;
        private string newId;

        public static void Show(Door door) {
            var w = CreateInstance<DoorChangeIdWindow>();
            w.door = door;
            w.newId = door != null ? door.DoorId : string.Empty;
            w.titleContent = new GUIContent("Change Door ID");
            w.minSize = new Vector2(520, 170);
            w.maxSize = new Vector2(520, 170);
            w.ShowUtility();
        }

        private void OnGUI() {
            if (door == null) {
                EditorGUILayout.HelpBox("Door reference is missing.", MessageType.Error);
                return;
            }

            EditorGUILayout.HelpBox(
                "Allowed: [0-9a-zA-Z_-], length 1..64\n" +
                "Change ID updates references in OPEN scenes (mark dirty) and in prefabs (auto-saved).",
                MessageType.Info
            );

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Current ID", door.DoorId);
            EditorGUI.EndDisabledGroup();

            newId = EditorGUILayout.TextField("New ID", newId);

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Random")) {
                    newId = DoorIdUtils.GenerateId(5);
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

            if (!DoorIdUtils.IsValidId(newId)) {
                EditorUtility.DisplayDialog("Invalid ID", "Allowed: [0-9a-zA-Z_-], length 1..64.", "OK");
                return;
            }

            var oldId = door.DoorId;
            if (newId == oldId) {
                Close();
                return;
            }

            var scene = door.gameObject.scene;
            if (!scene.IsValid()) {
                EditorUtility.DisplayDialog("Error", "Door is not in a valid scene.", "OK");
                return;
            }

            if (!DoorValidator.IsDoorIdUniqueInScene(scene, door, newId)) {
                EditorUtility.DisplayDialog("Duplicate ID", "This ID already exists in the same scene.", "OK");
                return;
            }

            var sceneGuid = DoorValidator.GetSceneGuid(scene.path);
            if (string.IsNullOrWhiteSpace(sceneGuid)) {
                EditorUtility.DisplayDialog("Error", "Failed to resolve scene GUID.", "OK");
                return;
            }

            var changedScenes = 0;
            var changedPrefabs = 0;

            DoorProjectUpdater.ReplaceReferencesInOpenScenes(sceneGuid, oldId, newId, ref changedScenes);
            DoorProjectUpdater.ReplaceReferencesInAllPrefabs(sceneGuid, oldId, newId, ref changedPrefabs);

            Undo.RecordObject(door, "Change Door ID");
            door.EditorSetDoorId(newId);
            EditorUtility.SetDirty(door);

            EditorSceneManager.MarkSceneDirty(scene);

            EditorUtility.DisplayDialog(
                "Done",
                $"Door ID changed: {oldId} -> {newId}\n" +
                $"Updated references:\n" +
                $"- Open scenes: {changedScenes} (scenes marked dirty)\n" +
                $"- Prefabs: {changedPrefabs} (prefabs saved)",
                "OK"
            );

            Close();
        }
    }
}
#endif
