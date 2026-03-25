using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Core.FSM.Editor {
    public abstract class RuntimeStateMachineInspector<TTarget> : UnityEditor.Editor where TTarget : UnityEngine.Object {
        private const string StatePropertyName = "State";
        private List<FieldInfo> stateMachineFields;
        private static readonly Type GenericStateMachineType = typeof(SimpleStateMachine<>);

        protected virtual void OnEnable() {
            stateMachineFields = GetStateMachineFields(target?.GetType());
        }

        public override bool RequiresConstantRepaint() {
            return Application.isPlaying;
        }

        public override void OnInspectorGUI() {
            DrawRuntimeStateMachineFields();
            DrawDefaultInspector();
        }

        protected virtual void DrawBeforeDefaultInspector(TTarget component) {
        }

        private void DrawRuntimeStateMachineFields() {
            if (!Application.isPlaying) {
                return;
            }

            var component = target as TTarget;
            if (component == null) {
                return;
            }

            DrawBeforeDefaultInspector(component);

            if (stateMachineFields == null || stateMachineFields.Count == 0) {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime State", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true)) {
                foreach (var field in stateMachineFields) {
                    var stateMachine = field.GetValue(component);
                    var value = GetStateDisplayValue(stateMachine, field.FieldType);
                    EditorGUILayout.TextField(ObjectNames.NicifyVariableName(field.Name), value);
                }
            }

            EditorGUILayout.Space();
        }

        private static List<FieldInfo> GetStateMachineFields(Type inspectedType) {
            var result = new List<FieldInfo>();

            if (inspectedType == null) {
                return result;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.DeclaredOnly;

            for (var type = inspectedType; type != null && type != typeof(MonoBehaviour); type = type.BaseType) {
                var fields = type.GetFields(flags);

                foreach (var field in fields) {
                    if (GetStateProperty(field.FieldType) != null) {
                        result.Add(field);
                    }
                }
            }

            return result;
        }

        private static string GetStateDisplayValue(object stateMachine, Type fieldType) {
            if (stateMachine == null) {
                return "(null)";
            }

            var stateProperty = GetStateProperty(fieldType);
            var stateValue = stateProperty?.GetValue(stateMachine);
            return stateValue?.ToString() ?? "(null)";
        }

        private static PropertyInfo GetStateProperty(Type type) {
            var genericStateMachineType = GetGenericStateMachineType(type);
            return genericStateMachineType?.GetProperty(StatePropertyName);
        }

        private static Type GetGenericStateMachineType(Type type) {
            for (var currentType = type; currentType != null; currentType = currentType.BaseType) {
                if (currentType.IsGenericType &&
                    currentType.GetGenericTypeDefinition() == GenericStateMachineType) {
                    return currentType;
                }
            }

            return null;
        }
    }
}
