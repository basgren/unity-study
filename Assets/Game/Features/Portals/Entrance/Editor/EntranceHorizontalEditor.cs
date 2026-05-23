#if UNITY_EDITOR
using Game.Features.Portals.Common.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.Features.Portals.Entrance.Editor {
    /// <summary>
    /// Custom inspector for <see cref="EntranceHorizontal"/>. Mirrors <see cref="EntranceEditor"/>'s
    /// id-foldout layout, then adds Scene-view position handles for the back-spawn and target
    /// offsets so they can be dragged directly in the editor without needing child Transforms.
    /// </summary>
    [CustomEditor(typeof(EntranceHorizontal))]
    public sealed class EntranceHorizontalEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            var entrance = (EntranceHorizontal)target;

            PortalInspectorFoldout.DrawIdFoldout(entrance, "EntranceHorizontal", entrance.EntranceId,
                entrance.EditorSetEntranceId);

            EditorGUILayout.Space();

            DrawPropertiesExcluding(serializedObject, "m_Script", "entranceId");

            serializedObject.ApplyModifiedProperties();
        }

        private void OnSceneGUI() {
            var entrance = (EntranceHorizontal)target;
            var transform = entrance.transform;

            // BackSpawn world position already accounts for Up/Down Y-mirroring via GetBackSpawnPosition().
            // The handle edits the configured offset though, so we read/write through the editor accessors
            // and translate via the same effective mapping when needed.
            DrawPointHandle(entrance, "BackSpawn",
                entrance.GetBackSpawnPosition(),
                new Color(0.4f, 0.6f, 1f, 1f),
                world => {
                    var localOffset = (Vector2)transform.InverseTransformPoint(world);
                    if (entrance.Direction == HorizontalPortalDirection.Down) {
                        localOffset = new Vector2(localOffset.x, -localOffset.y);
                    }
                    entrance.EditorSetBackSpawnOffset(localOffset);
                });

            // Target handles are only meaningful for Up portals; Down portals ignore them entirely.
            if (entrance.Direction != HorizontalPortalDirection.Up) {
                return;
            }

            if (entrance.HasLeftTarget) {
                DrawPointHandle(entrance, "Left Target",
                    entrance.GetLeftTargetPosition(),
                    new Color(1f, 0.85f, 0f, 1f),
                    world => entrance.EditorSetLeftTargetOffset(transform.InverseTransformPoint(world)));
            }

            if (entrance.HasRightTarget) {
                DrawPointHandle(entrance, "Right Target",
                    entrance.GetRightTargetPosition(),
                    new Color(1f, 0.85f, 0f, 1f),
                    world => entrance.EditorSetRightTargetOffset(transform.InverseTransformPoint(world)));
            }
        }

        // Compact handle layout: short X/Y axis sliders + a free-drag dot in the center. Much
        // smaller than Handles.PositionHandle so the entrance's own transform gizmo stays readable.
        private const float AxisLengthScale = 0.4f;
        private const float DotSizeScale = 0.08f;

        private void DrawPointHandle(Object owner, string label, Vector3 worldPos, Color color,
            System.Action<Vector3> apply) {
            var handleSize = HandleUtility.GetHandleSize(worldPos);
            var axisLen = handleSize * AxisLengthScale;
            var dotSize = handleSize * DotSizeScale;

            Handles.color = color;
            Handles.Label(worldPos + (Vector3.up * (axisLen + 0.1f)), label);

            EditorGUI.BeginChangeCheck();

            Handles.color = Handles.xAxisColor;
            var afterX = Handles.Slider(worldPos, Vector3.right, axisLen, Handles.ArrowHandleCap, 0f);

            Handles.color = Handles.yAxisColor;
            var afterY = Handles.Slider(afterX, Vector3.up, axisLen, Handles.ArrowHandleCap, 0f);

            Handles.color = color;
            var afterFree = Handles.FreeMoveHandle(afterY, dotSize, Vector3.zero, Handles.DotHandleCap);

            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(owner, "Move " + label);
                // Lock Z to the entrance's plane to keep 2D pixel-perfect setup intact.
                afterFree.z = worldPos.z;
                apply(afterFree);
                EditorUtility.SetDirty(owner);
            }
        }
    }
}
#endif
