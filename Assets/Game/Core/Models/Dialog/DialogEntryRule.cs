using System;

namespace Game.Core.Models.Dialog {
    /// <summary>
    /// A conditional entry point for a dialog. The dialog opens at the first rule
    /// whose conditions are met and whose target node is not a spent one-shot.
    /// </summary>
    [Serializable]
    public class DialogEntryRule {
        public string nodeId;
        public DialogCondition[] conditions;
    }
}
