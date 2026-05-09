using Game.Features.Bosses.VengefulSpirit.AI;
using Game.Features.Bosses.VengefulSpirit.AI.Patterns;
using UnityEditor;
using UnityEngine;

namespace Game.Features.Bosses.VengefulSpirit.Editor {
    /// <summary>
    /// Custom inspector for <see cref="VengefulSpiritAI"/>. Adds a play-mode debug panel
    /// under the default fields showing current phase, last/active pattern, and per-slot
    /// usage for the active phase pool. Repaints every frame so the panel stays live.
    /// </summary>
    [CustomEditor(typeof(VengefulSpiritAI))]
    public class VengefulSpiritAIEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            DrawDefaultInspector();

            if (!Application.isPlaying) {
                return;
            }

            VengefulSpiritAI ai = (VengefulSpiritAI)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime State", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true)) {
                EditorGUILayout.LabelField("Phase", ai.CurrentPhase.ToString());

                VengefulSpiritPattern active = ai.ActivePattern;
                EditorGUILayout.LabelField("Active Pattern", active != null ? active.Id : "<idle>");

                VengefulSpiritPattern last = ai.LastPattern;
                string lastLabel = last != null
                    ? $"{last.Id} (x{ai.LastPatternConsecutive})"
                    : "<none>";
                EditorGUILayout.LabelField("Last Pattern", lastLabel);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Active Pool ({ai.CurrentPhase})", EditorStyles.boldLabel);

            BossPatternSlot[] pool = ai.GetActivePool();
            if (pool == null || pool.Length == 0) {
                EditorGUILayout.LabelField("  (empty)");
            } else {
                for (int i = 0; i < pool.Length; i++) {
                    DrawSlotRow(ai, pool[i]);
                }
            }

            // Live updates so designers see the panel breathe during play.
            Repaint();
        }

        private static void DrawSlotRow(VengefulSpiritAI ai, BossPatternSlot slot) {
            VengefulSpiritPattern p = slot.pattern;
            if (p == null) {
                EditorGUILayout.LabelField("  <unwired slot>");
                return;
            }

            int used = ai.PatternUsage.TryGetValue(p, out int u) ? u : 0;
            string capStr = slot.maxPerCycle < 0 ? "∞" : slot.maxPerCycle.ToString();
            string inARowStr = slot.maxInARow < 0 ? "∞" : slot.maxInARow.ToString();

            bool capExhausted = slot.maxPerCycle > 0 && used >= slot.maxPerCycle;
            bool inARowBlocked = slot.maxInARow >= 0
                                 && p == ai.LastPattern
                                 && ai.LastPatternConsecutive >= slot.maxInARow;
            bool disabled = slot.maxPerCycle == 0;
            bool runnable = !capExhausted && !inARowBlocked && !disabled;

            string tag;
            if (disabled) {
                tag = "[disabled]";
            } else if (capExhausted) {
                tag = "[cap]";
            } else if (inARowBlocked) {
                tag = "[in-a-row]";
            } else {
                tag = "[ready]";
            }

            using (new EditorGUI.DisabledScope(!runnable)) {
                EditorGUILayout.LabelField($"  {p.Id}  used {used}/{capStr}  inARow:{inARowStr}  {tag}");
            }
        }
    }
}
