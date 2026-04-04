using System;

namespace Game.Core.Models.Dialog {
    [Serializable]
    public class DialogChoice {
        public string textKey;
        public string nextNodeId;
        public DialogCondition[] conditions;
        public DialogAction[] actions;
    }
}
