using System;

namespace Game.Core.Models.Dialog {
    [Serializable]
    public class DialogChoice {
        public string text;
        public string nextNodeId;
        public DialogCondition[] conditions;
        public DialogAction[] actions;
    }
}
