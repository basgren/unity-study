using System;

namespace Game.Core.Models.Dialog {
    [Serializable]
    public class DialogNode {
        public string nodeId;
        public string speaker;
        public DialogLine[] lines;
        public DialogChoice[] choices;
        public DialogAction[] onEnterActions;
    }
}
