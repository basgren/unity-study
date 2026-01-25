/*
ApplyDefaultsToSelectedTransitions.cs
====================================

Purpose
-------
Applies a predefined set of default settings to Animator transitions that are currently
selected in the Animator window.

This is useful in Unity 2019 where Presets may overwrite the Destination State reference,
and where there is no built-in way to set transition defaults at creation time.

What this tool does
-------------------
For every selected AnimatorStateTransition:
- Has Exit Time       = true
- Exit Time           = 1.0
- Fixed Duration      = false
- Transition Duration = 0.0

Usage
-----
1) Put this script into: Assets/Editor/
2) Open an Animator Controller, select one or multiple transitions (arrows) in the Animator window.
3) Run: Tools -> Animator -> Apply Defaults To Selected Transitions
4) Undo is supported (Ctrl+Z).

Notes
-----
- This tool modifies ONLY the selected transitions.
- It does not touch Destination State (it doesn't even read or write it).
- Works for normal state transitions and AnyState transitions (both are AnimatorStateTransition).
*/

using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Editor {
    public static class ApplyDefaultsToSelectedTransitions {
        private const bool HasExitTime = true;
        private const float ExitTime = 1f;
        private const bool HasFixedDuration = false;
        private const float Duration = 0f;

        [MenuItem("Tools/Animator/Apply Defaults To Selected Transitions")]
        private static void Apply() {
            var transitions = GetSelectedTransitions();
            if (transitions.Length == 0) {
                Debug.LogWarning(
                    "No AnimatorStateTransition selected. Select transition arrows in the Animator window.");
                return;
            }

            Undo.RecordObjects(transitions, "Apply Transition Defaults");

            var changed = 0;

            for (int i = 0; i < transitions.Length; i++) {
                var tr = transitions[i];

                tr.hasExitTime = HasExitTime;
                tr.exitTime = ExitTime;
                tr.hasFixedDuration = HasFixedDuration;
                tr.duration = Duration;

                EditorUtility.SetDirty(tr);
                changed++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Applied defaults to {changed} transition(s).");
        }

        [MenuItem("Tools/Animator/Apply Defaults To Selected Transitions", true)]
        private static bool Apply_Validate() {
            return GetSelectedTransitions().Length > 0;
        }

        private static AnimatorStateTransition[] GetSelectedTransitions() {
            var objs = Selection.objects;
            if (objs == null || objs.Length == 0) {
                return new AnimatorStateTransition[0];
            }

            // Count first to avoid allocations from LINQ.
            var count = 0;
            for (int i = 0; i < objs.Length; i++) {
                if (objs[i] is AnimatorStateTransition) {
                    count++;
                }
            }

            if (count == 0) {
                return new AnimatorStateTransition[0];
            }

            var result = new AnimatorStateTransition[count];
            var idx = 0;

            for (int i = 0; i < objs.Length; i++) {
                var tr = objs[i] as AnimatorStateTransition;
                if (tr != null) {
                    result[idx] = tr;
                    idx++;
                }
            }

            return result;
        }
    }
}
