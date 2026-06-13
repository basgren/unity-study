using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Tools {
    /// <summary>
    /// Custom inspector for <see cref="PolygonCollider2D"/> that wraps Unity's built-in editor and
    /// appends a grid-snap tool. The tool snaps every collider point to the nearest world-space grid
    /// point at a chosen grid size, which is handy for aligning level geometry to block boundaries.
    ///
    /// The native inspector (and its "Edit Collider" scene handles) is preserved by instantiating and
    /// forwarding to the internal <c>UnityEditor.PolygonCollider2DEditor</c> via reflection. If that
    /// internal type cannot be found on a future Unity version, the wrapper degrades gracefully to the
    /// default inspector so the collider is never left un-editable.
    /// </summary>
    [CustomEditor(typeof(PolygonCollider2D))]
    [CanEditMultipleObjects]
    public class PolygonCollider2DGridSnapEditor : UnityEditor.Editor {
        private const string GridIndexPrefKey = "PolygonCollider2DGridSnap.GridIndex";

        private static readonly string[] GridLabels = { "0.1", "0.25", "0.5", "1" };
        private static readonly float[] GridValues = { 0.1f, 0.25f, 0.5f, 1f };

        private UnityEditor.Editor nativeEditor;
        private MethodInfo nativeOnSceneGui;
        private int gridIndex;

        private void OnEnable() {
            gridIndex = Mathf.Clamp(EditorPrefs.GetInt(GridIndexPrefKey, 2), 0, GridValues.Length - 1);

            Type nativeType = Type.GetType("UnityEditor.PolygonCollider2DEditor, UnityEditor");
            if (nativeType == null) {
                return;
            }

            nativeEditor = CreateEditor(targets, nativeType);
            nativeOnSceneGui = nativeType.GetMethod("OnSceneGUI", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        }

        private void OnDisable() {
            if (nativeEditor != null) {
                DestroyImmediate(nativeEditor);
                nativeEditor = null;
            }
        }

        public override void OnInspectorGUI() {
            // Draw Unity's own inspector first so the standard collider UI stays intact.
            if (nativeEditor != null) {
                nativeEditor.OnInspectorGUI();
            } else {
                DrawDefaultInspector();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Grid Snap", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            gridIndex = EditorGUILayout.Popup("Grid Size", gridIndex, GridLabels);
            if (EditorGUI.EndChangeCheck()) {
                EditorPrefs.SetInt(GridIndexPrefKey, gridIndex);
            }

            if (GUILayout.Button("Snap Points To Grid")) {
                SnapAllTargets(GridValues[gridIndex]);
            }
        }

        // Forward scene handles to the native editor so "Edit Collider" point editing keeps working.
        private void OnSceneGUI() {
            if (nativeEditor != null && nativeOnSceneGui != null) {
                nativeOnSceneGui.Invoke(nativeEditor, null);
            }
        }

        private void SnapAllTargets(float grid) {
            if (grid <= 0f) {
                return;
            }

            foreach (UnityEngine.Object obj in targets) {
                PolygonCollider2D collider = obj as PolygonCollider2D;
                if (collider == null) {
                    continue;
                }

                SnapCollider(collider, grid);
            }
        }

        private void SnapCollider(PolygonCollider2D collider, float grid) {
            Undo.RecordObject(collider, "Snap Collider Points To Grid");

            Transform transform = collider.transform;
            Vector2 offset = collider.offset;

            for (int pathIndex = 0; pathIndex < collider.pathCount; pathIndex++) {
                Vector2[] path = collider.GetPath(pathIndex);

                for (int i = 0; i < path.Length; i++) {
                    // Local (including collider offset) -> world, round to grid, then back to local.
                    Vector3 world = transform.TransformPoint(path[i] + offset);
                    world.x = Mathf.Round(world.x / grid) * grid;
                    world.y = Mathf.Round(world.y / grid) * grid;
                    path[i] = (Vector2)transform.InverseTransformPoint(world) - offset;
                }

                collider.SetPath(pathIndex, path);
            }

            EditorUtility.SetDirty(collider);
        }
    }
}
