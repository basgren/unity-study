using System;

namespace Game.Core.Models.Dialog {
    public enum ConditionType {
        HasItem,
        DoesNotHaveItem,
        FlagSet,
        FlagNotSet,
        IsArmed,
    }

    [Serializable]
    public class DialogCondition {
        public ConditionType type;
        public string stringParam;
        public int intParam;
    }
}
