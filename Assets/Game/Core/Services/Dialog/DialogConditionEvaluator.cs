using Game.Core.Models.Dialog;
using Game.Features.Characters.Hero;
using UnityEngine;

namespace Game.Core.Services.Dialog {
    /// <summary>
    /// Pure evaluation of dialog conditions against the current player state.
    /// Shared by entry-rule resolution and choice filtering.
    /// </summary>
    public static class DialogConditionEvaluator {
        public static bool AreConditionsMet(DialogCondition[] conditions, PlayerState state) {
            if (conditions == null || conditions.Length == 0) {
                return true;
            }

            for (int i = 0; i < conditions.Length; i++) {
                if (!EvaluateCondition(conditions[i], state)) {
                    return false;
                }
            }

            return true;
        }

        private static bool EvaluateCondition(DialogCondition cond, PlayerState state) {
            switch (cond.type) {
                case ConditionType.HasItem:
                    var minCount = Mathf.Max(cond.intParam, 1);
                    return state.InventoryModel.GetCount(cond.stringParam) >= minCount;

                case ConditionType.DoesNotHaveItem:
                    // If intParam is set, treat this as "has fewer than N items" for dialog fallback branches.
                    var maxCountExclusive = Mathf.Max(cond.intParam, 1);
                    return state.InventoryModel.GetCount(cond.stringParam) < maxCountExclusive;

                case ConditionType.FlagSet:
                    return state.HasFlag(cond.stringParam);

                case ConditionType.FlagNotSet:
                    return !state.HasFlag(cond.stringParam);

                case ConditionType.IsArmed:
                    return state.IsArmed;

                default:
                    Debug.LogWarning($"DialogConditionEvaluator: unknown condition type '{cond.type}'.");
                    return false;
            }
        }
    }
}
