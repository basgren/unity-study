using EditorOnly;
using UnityEditor;
using UnityEngine;

namespace Editor {
    /// <summary>
    /// Adds a “Scene Note” creation command to the GameObject/Create menu.
    /// Scene Notes are automatically placed inside a root container called “Notes”
    /// and named incrementally (Note01, Note02…).
    /// </summary>
    public static class SceneNoteMenu {
        private const string NotesContainerName = "Notes";

        [MenuItem("GameObject/Scene Note", false, 10)]
        private static void CreateSceneNote(MenuCommand menuCommand) {
            GameObject notesRoot = GetOrCreateNotesRoot();
            string noteName = GetNextNoteName(notesRoot);

            GameObject noteObject = CreateNoteObject(noteName, notesRoot);
            PlaceNoteAtSceneView(noteObject, menuCommand);

            Selection.activeObject = noteObject;
        }

        private static GameObject GetOrCreateNotesRoot() {
            GameObject root = GameObject.Find(NotesContainerName);
            if (root == null) {
                root = new GameObject(NotesContainerName);
                Undo.RegisterCreatedObjectUndo(root, "Create Notes Root");
            }

            return root;
        }

        private static string GetNextNoteName(GameObject notesRoot) {
            int maxIndex = 0;

            foreach (Transform child in notesRoot.transform) {
                if (!child.name.StartsWith("Note"))
                    continue;

                string digits = child.name.Substring(4);
                if (int.TryParse(digits, out int number)) {
                    if (number > maxIndex)
                        maxIndex = number;
                }
            }

            return $"Note{(maxIndex + 1):D2}";
        }

        private static GameObject CreateNoteObject(string name, GameObject parent) {
            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Scene Note");

            go.AddComponent<SceneNote>();
            Undo.SetTransformParent(go.transform, parent.transform, "Assign Note Parent");

            SceneNote note = go.GetComponent<SceneNote>();
            note.text = "Note";
            note.showTextInScene = true;

            return go;
        }

        private static void PlaceNoteAtSceneView(GameObject noteObject, MenuCommand menuCommand) {
            if (menuCommand.context is GameObject ctx) {
                noteObject.transform.position = ctx.transform.position;
                return;
            }

            if (SceneView.lastActiveSceneView != null) {
                noteObject.transform.position = SceneView.lastActiveSceneView.pivot;
            }
        }
    }
}
