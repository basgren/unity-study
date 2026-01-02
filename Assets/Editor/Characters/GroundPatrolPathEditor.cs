using Prefabs.Characters.Sharky;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Editor.Characters {
    [CustomEditor(typeof(GroundPatrolPath))]
    public class GroundPatrolPathEditor : UnityEditor.Editor {
        private SerializedProperty startIndex;
        private SerializedProperty direction;
        private SerializedProperty points;

        private ReorderableList pointsList;

        private void OnEnable() {
            startIndex = serializedObject.FindProperty("startIndex");
            direction = serializedObject.FindProperty("direction");
            points = serializedObject.FindProperty("points");

            pointsList = new ReorderableList(serializedObject, points, true, true, false, true);

            pointsList.drawHeaderCallback = (rect) => { EditorGUI.LabelField(rect, "Patrol Points (World)"); };

            pointsList.elementHeight = EditorGUIUtility.singleLineHeight + 6f;

            pointsList.drawElementCallback = (rect, index, active, focused) => {
                rect.y += 2f;

                var element = points.GetArrayElementAtIndex(index);
                var posProp = element.FindPropertyRelative("position");
                var delayProp = element.FindPropertyRelative("delay");

                var xProp = posProp.FindPropertyRelative("x");
                var yProp = posProp.FindPropertyRelative("y");

                rect = EditorGUI.IndentedRect(rect);

                var line = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);

                var idxRect = new Rect(line.x, line.y, 28f, line.height);
                EditorGUI.LabelField(idxRect, index.ToString(), EditorStyles.miniLabel);
                line.x = idxRect.xMax + 6f;

                DrawLabeledFloat(ref line, "X", xProp, 14f, 75f);
                line.x += 8f;
                DrawLabeledFloat(ref line, "Y", yProp, 14f, 75f);
                line.x += 8f;
                DrawLabeledFloat(ref line, "D", delayProp, 14f, 60f);
            };
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();

            EditorGUILayout.PropertyField(direction);
            
            DrawStartIndexField();

            pointsList.DoLayoutList();

            if (GUILayout.Button("Add Point")) {
                AddPointAtEnemyPlusOneX();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawStartIndexField() {
            var count = points.arraySize;

            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.PrefixLabel("Start Index");

                using (new EditorGUI.DisabledGroupScope(count == 0)) {
                    var max = Mathf.Max(0, count - 1);
                    var value = startIndex.intValue;

                    value = EditorGUILayout.IntSlider(value, 0, max);
                    startIndex.intValue = Mathf.Clamp(value, 0, max);
                }
            }
        }

        private void OnSceneGUI() {
            serializedObject.Update();

            Handles.color = Color.white;

            for (var i = 0; i + 1 < points.arraySize; i++) {
                var a = points.GetArrayElementAtIndex(i).FindPropertyRelative("position").vector2Value;
                var b = points.GetArrayElementAtIndex(i + 1).FindPropertyRelative("position").vector2Value;
                Handles.DrawLine(a, b);
            }

            for (var i = 0; i < points.arraySize; i++) {
                var element = points.GetArrayElementAtIndex(i);
                var posProp = element.FindPropertyRelative("position");

                var world = posProp.vector2Value;

                // Draw the index label slightly below the point to reduce overlap.
                Handles.Label(world + Vector2.down * 0.18f, i.ToString());

                EditorGUI.BeginChangeCheck();

                // Draw a small 2D handle (no axes).
                var size = HandleUtility.GetHandleSize(world) * 0.08f;
                var newWorld3 = Handles.FreeMoveHandle(
                    world,
                    Quaternion.identity,
                    size,
                    Vector3.zero,
                    Handles.SphereHandleCap
                );

                if (EditorGUI.EndChangeCheck()) {
                    Undo.RecordObject(target, "Move Patrol Point");
                    posProp.vector2Value = (Vector2)newWorld3;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void AddPointAtEnemyPlusOneX() {
            serializedObject.Update();

            var path = (GroundPatrolPath)target;
            var enemyPos = (Vector2)path.transform.position;
            var newPos = enemyPos + Vector2.right;

            Undo.RecordObject(path, "Add Patrol Point");

            points.arraySize += 1;
            var element = points.GetArrayElementAtIndex(points.arraySize - 1);
            element.FindPropertyRelative("position").vector2Value = newPos;
            element.FindPropertyRelative("delay").floatValue = 0f;

            // Clamp start index if needed.
            if (points.arraySize == 1) {
                startIndex.intValue = 0;
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(path);
        }

        private static void DrawLabeledFloat(ref Rect line, string label, SerializedProperty prop, float labelWidth,
            float fieldWidth) {
            var labelRect = new Rect(line.x, line.y, labelWidth, line.height);
            var fieldRect = new Rect(labelRect.xMax, line.y, fieldWidth, line.height);

            EditorGUI.LabelField(labelRect, label + ":", EditorStyles.miniLabel);
            EditorGUI.PropertyField(fieldRect, prop, GUIContent.none);

            line.x = fieldRect.xMax;
        }
    }
}
