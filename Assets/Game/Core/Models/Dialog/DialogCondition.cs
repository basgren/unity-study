using System;

namespace Game.Core.Models.Dialog {
    public enum ConditionType {
        HasItem,
        DoesNotHaveItem,
        FlagSet,
        FlagNotSet,
    }

    [Serializable]
    public class DialogCondition {
        public ConditionType type;
        public string stringParam;
        public int intParam;
    }
}
